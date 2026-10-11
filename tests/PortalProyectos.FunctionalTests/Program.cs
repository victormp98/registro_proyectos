using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.WebSockets;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using PortalProyectos.Web.Models.Account;
using PortalProyectos.Application.Catalogs;
using PortalProyectos.Application.Notifications;
using PortalProyectos.Application.News;
using PortalProyectos.Application.Projects;
using PortalProyectos.Application.Users;
using PortalProyectos.Domain.Notifications;
using PortalProyectos.Domain.Files;
using PortalProyectos.Domain.Catalogs;
using PortalProyectos.Domain.Projects;
using PortalProyectos.Domain.Security;
using PortalProyectos.Domain.Users;
using PortalProyectos.Infrastructure;
using PortalProyectos.Infrastructure.Identity;
using PortalProyectos.Infrastructure.Persistence;
using PortalProyectos.Web.Hubs;

var testRoot = Path.Combine(Path.GetTempPath(), "PortalProyectosFunctionalTests", Guid.NewGuid().ToString("N"));
// Optional disposable fixture for the separate browser review. Never uses an existing installation.
var retainUiFixture = args.Contains("--retain-ui-fixture", StringComparer.Ordinal);
Directory.CreateDirectory(testRoot);
var databasePath = Path.Combine(testRoot, "functional-tests.db");
var storageRoot = Path.Combine(testRoot, "storage");
var testPublisher = new RecordingNotificationPublisher();
var testFileStorage = new FunctionalTestFileStorageService(storageRoot);
ServiceProvider? provider = null;

try
{
    var registrationModel = new RegisterViewModel();
    var privacyProperty = typeof(RegisterViewModel).GetProperty(nameof(RegisterViewModel.AcceptPrivacy))!;
    var privacyRule = privacyProperty.GetCustomAttributes<ValidationAttribute>().Single();
    var privacyContext = new ValidationContext(registrationModel) { MemberName = nameof(RegisterViewModel.AcceptPrivacy) };
    Check(privacyRule.GetValidationResult(false, privacyContext) != ValidationResult.Success
        && privacyRule.GetValidationResult(true, privacyContext) == ValidationResult.Success,
        "PRIVACIDAD", "La aceptación desmarcada se rechaza y la marcada se acepta en servidor");
    var auditActionParameter = typeof(PortalProyectos.Web.Areas.Admin.Controllers.AuditLogController)
        .GetMethod("Index")!.GetParameters().Single(x => x.Name == "actionFilter");
    Check(auditActionParameter.GetCustomAttribute<FromQueryAttribute>()?.Name == "action",
        "BITÁCORA BINDING", "El filtro action se liga desde query y no del valor de ruta MVC");
    var resetStudentAction = typeof(PortalProyectos.Web.Areas.Admin.Controllers.UsersController).GetMethod("ResetPassword")!;
    Check(resetStudentAction.GetCustomAttributes<HttpPostAttribute>().Any()
        && typeof(PortalProyectos.Web.Areas.Admin.Controllers.UsersController).GetCustomAttribute<AuthorizeAttribute>()?.Policy == "Administracion",
        "RESTABLECIMIENTO ALUMNO", "El endpoint de contraseña temporal está protegido y acepta POST");

    var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:PortalDatabase"] = $"Data Source={databasePath};Cache=Shared;Foreign Keys=True",
        ["Portal:Projects:MaxParticipants"] = "6",
        ["Portal:Projects:MaxActiveProjects"] = "3",
        ["Storage:RootPath"] = storageRoot,
        ["Storage:MaxProjectPdfBytes"] = "10485760"
    }).Build();

    var services = new ServiceCollection();
    services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
    services.AddSingleton<IHostEnvironment>(new FunctionalTestHostEnvironment(Path.Combine(testRoot, "application")));
    services.AddInfrastructure(configuration);
    services.AddSingleton<INotificationPublisher>(testPublisher);
    services.AddSingleton<IFileStorageService>(testFileStorage);
    provider = services.BuildServiceProvider();

    await using (var seedScope = provider.CreateAsyncScope())
        await seedScope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();

    ApplicationUser pending;
    ApplicationUser owner;
    ApplicationUser linked;
    ApplicationUser other;
    ApplicationUser adminOne;
    ApplicationUser adminTwo;
    int subjectId;
    int projectTypeId;
    int schoolCycleId;
    int careerId;
    int otherCareerId;
    int gradeId;
    int groupId;

    await using (var setupScope = provider.CreateAsyncScope())
    {
        var db = setupScope.ServiceProvider.GetRequiredService<PortalDbContext>();
        careerId = await db.Careers.Select(x => x.Id).FirstAsync();
        var otherCareer = new Career { Name = "Carrera de prueba adicional", Code = "CPA" };
        var inactiveCareer = new Career { Name = "Carrera inactiva de prueba", Code = "CIP", IsActive = false };
        db.Careers.AddRange(otherCareer, inactiveCareer);
        await db.SaveChangesAsync();
        otherCareerId = otherCareer.Id;
        db.Subjects.AddRange(
            new Subject { Name = "Materia de otra carrera", Code = "MOC", CareerId = otherCareer.Id },
            new Subject { Name = "Materia inactiva", Code = "MIN", CareerId = careerId, IsActive = false },
            new Subject { Name = "Materia de carrera inactiva", Code = "MCI", CareerId = inactiveCareer.Id });
        await db.SaveChangesAsync();
        gradeId = await db.Grades.OrderBy(x => x.SortOrder).Select(x => x.Id).FirstAsync();
        groupId = await db.AcademicGroups.OrderBy(x => x.SortOrder).Select(x => x.Id).FirstAsync();
        subjectId = await db.Subjects.Select(x => x.Id).FirstAsync();
        projectTypeId = await db.ProjectTypes.Select(x => x.Id).FirstAsync();
        schoolCycleId = await db.SchoolCycles.Select(x => x.Id).FirstAsync();
        var users = setupScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        pending = await CreateUserAsync(users, "PEND001", "Alumno Pendiente", "pendiente@test.local", AccountStatus.Pending, SystemRoles.Student, careerId, gradeId, groupId);
        owner = await CreateUserAsync(users, "OWNER001", "Alumno Responsable", "responsable@test.local", AccountStatus.Approved, SystemRoles.Student, careerId, gradeId, groupId);
        linked = await CreateUserAsync(users, "LINK001", "Alumno Vinculado", "vinculado@test.local", AccountStatus.Approved, SystemRoles.Student, careerId, gradeId, groupId);
        other = await CreateUserAsync(users, "OTHER001", "Alumno Distinto", "distinto@test.local", AccountStatus.Approved, SystemRoles.Student, careerId, gradeId, groupId);
        adminOne = await CreateUserAsync(users, "ADMIN001", "Administrador Uno", "admin1@test.local", AccountStatus.Approved, SystemRoles.Administrator);
        adminTwo = await CreateUserAsync(users, "ADMIN002", "Administrador Dos", "admin2@test.local", AccountStatus.Approved, SystemRoles.Administrator);
    }

    await using (var passwordScope = provider.CreateAsyncScope())
    {
        var users = passwordScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var resetStudent = await CreateUserAsync(users, "RESET001", "Alumno Para Restablecer", "reset@test.local", AccountStatus.Approved, SystemRoles.Student);
        var admin = await users.FindByIdAsync(adminOne.Id) ?? throw new InvalidOperationException("Administrador de prueba ausente.");
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, admin.Id)], "FunctionalTest"))
        };
        var usersController = new PortalProyectos.Web.Areas.Admin.Controllers.UsersController(
            passwordScope.ServiceProvider.GetRequiredService<IAccountAdministrationService>(),
            users,
            passwordScope.ServiceProvider.GetRequiredService<PortalDbContext>())
        {
            ControllerContext = new ControllerContext { HttpContext = context },
            TempData = new TempDataDictionary(context, new FunctionalTestTempDataProvider())
        };
        var resetResult = await usersController.ResetPassword(resetStudent.Id);
        var credential = usersController.TempData["OneTimeCredential"] as string;
        var temporaryPassword = credential?.Split(": ", StringSplitOptions.None).LastOrDefault();
        var updatedOwner = await users.FindByIdAsync(resetStudent.Id);
        var resetAuditExists = await passwordScope.ServiceProvider.GetRequiredService<PortalDbContext>().AuditLogs
            .AnyAsync(x => x.Action == "Alumno.ContrasenaRestablecida" && x.EntityId == resetStudent.Id);
        Check(resetResult is RedirectToActionResult && !string.IsNullOrWhiteSpace(temporaryPassword)
            && updatedOwner?.MustChangePassword == true && await users.CheckPasswordAsync(updatedOwner, temporaryPassword)
            && resetAuditExists,
            "RESET CONTRASEÑA ALUMNO", "Se genera una credencial temporal de un solo uso, cambio obligatorio y bitácora");
    }

    await using var mainScope = provider.CreateAsyncScope();
    var studentProjects = mainScope.ServiceProvider.GetRequiredService<IStudentProjectService>();
    var reviews = mainScope.ServiceProvider.GetRequiredService<IProjectReviewService>();
    var projectCatalogs = mainScope.ServiceProvider.GetRequiredService<IProjectCatalogService>();

    var careerSubjects = await projectCatalogs.GetProjectFormCatalogsAsync(careerId);
    var otherCareerSubjects = await projectCatalogs.GetProjectFormCatalogsAsync(otherCareerId);
    Check(careerSubjects.Subjects.Count >= 1 && careerSubjects.Subjects.All(x => x.CareerId == careerId && x.IsActive)
        && careerSubjects.Subjects.All(x => x.Name != "Materia de otra carrera" && x.Name != "Materia inactiva" && x.Name != "Materia de carrera inactiva")
        && otherCareerSubjects.Subjects.Count == 1 && otherCareerSubjects.Subjects[0].Name == "Materia de otra carrera",
        "MATERIAS POR CARRERA", "El formulario ofrece solo materias activas de la carrera activa indicada");
    await using (var catalogScope = provider.CreateAsyncScope())
    {
        var db = catalogScope.ServiceProvider.GetRequiredService<PortalDbContext>();
        var career = await db.Careers.SingleAsync(x => x.Id == careerId);
        career.Name = "Carrera renombrada en prueba";
        career.Code = "CRP";
        await db.SaveChangesAsync();
    }
    var renamedCareerSubjects = await projectCatalogs.GetProjectFormCatalogsAsync(careerId);
    Check(renamedCareerSubjects.Subjects.Count == careerSubjects.Subjects.Count
        && renamedCareerSubjects.Subjects.All(x => x.Career == "Carrera renombrada en prueba"),
        "MATERIAS TRAS EDITAR CARRERA", "Editar nombre o clave conserva la relación por CareerId");

    Check(typeof(IStudentProjectService).GetMethod("AddManualParticipantAsync") is null
        && typeof(PortalProyectos.Web.Areas.Student.Controllers.ProjectsController).GetMethod("AddManualParticipant") is null,
        "PARTICIPANTE MANUAL RETIRADO", "No existe contrato de servicio ni endpoint para crear participantes sin cuenta");

    var pendingCreate = await studentProjects.CreateDraftAsync(pending.Id, pending.FullName);
    Check(!pendingCreate.Succeeded, "CASO 1", "Alumno pendiente no puede crear proyecto");

    var draftCreate = await studentProjects.CreateDraftAsync(owner.Id, owner.FullName);
    Check(draftCreate.Succeeded && draftCreate.Value > 0, "CASO 2", "Alumno aprobado crea borrador");
    var projectId = draftCreate.Value;

    var incompleteSubmit = await studentProjects.SubmitAsync(owner.Id, owner.FullName, projectId);
    Check(!incompleteSubmit.Succeeded, "CASO 3", "Borrador incompleto no puede enviarse");

    var documents = mainScope.ServiceProvider.GetRequiredService<IProjectDocumentService>();
    var initialLookup = await studentProjects.FindStudentAsync(owner.Id, projectId, linked.EnrollmentNumber);
    var initialAddParticipant = await studentProjects.AddLinkedParticipantAsync(owner.Id, owner.FullName, projectId, linked.Id);
    var initialUpload = await documents.UploadAsync(owner.Id, owner.FullName, projectId, "pre-save.pdf", "application/pdf", 5, new MemoryStream(System.Text.Encoding.ASCII.GetBytes("%PDF-")));
    Check(!initialLookup.Succeeded && initialLookup.Message.Contains("Guarda primero", StringComparison.OrdinalIgnoreCase)
        && !initialAddParticipant.Succeeded && initialAddParticipant.Message.Contains("Guarda primero", StringComparison.OrdinalIgnoreCase)
        && !initialUpload.Succeeded && initialUpload.Message.Contains("Guarda primero", StringComparison.OrdinalIgnoreCase),
        "PROYECTO SECUNDARIOS BLOQUEADOS", "Participantes y documentación no se pueden guardar antes de persistir los datos básicos");

    var validDraft = ValidDraft("Sistema funcional de seguimiento de proyectos", subjectId, projectTypeId, schoolCycleId);
    Check((await studentProjects.UpdateDraftAsync(owner.Id, projectId, validDraft)).Succeeded, "PREPARACIÓN", "Datos básicos persistidos antes de habilitar elementos secundarios");

    var beforeParticipantChecks = await studentProjects.GetProjectAsync(owner.Id, projectId);
    var missingParticipant = await studentProjects.FindStudentAsync(owner.Id, projectId, "MISSING01");
    var pendingParticipant = await studentProjects.FindStudentAsync(owner.Id, projectId, pending.EnrollmentNumber);
    var pendingAdd = await studentProjects.AddLinkedParticipantAsync(owner.Id, owner.FullName, projectId, pending.Id);
    Check(!missingParticipant.Succeeded && missingParticipant.Message.Contains("debe registrarse primero", StringComparison.OrdinalIgnoreCase)
        && !pendingParticipant.Succeeded && pendingParticipant.Message.Contains("debe ser aprobada", StringComparison.OrdinalIgnoreCase)
        && !pendingAdd.Succeeded && (await studentProjects.GetProjectAsync(owner.Id, projectId))!.Participants.Count == beforeParticipantChecks!.Participants.Count,
        "PARTICIPANTE NO REGISTRADO/PENDIENTE", "Sin cuenta se indica registro; cuenta pendiente no se puede vincular ni agregar");

    var participantCount = beforeParticipantChecks!.Participants.Count;
    var found = await studentProjects.FindStudentAsync(owner.Id, projectId, linked.EnrollmentNumber);
    var beforeConfirmation = await studentProjects.GetProjectAsync(owner.Id, projectId);
    var confirmed = await studentProjects.AddLinkedParticipantAsync(owner.Id, owner.FullName, projectId, linked.Id);
    var afterConfirmation = await studentProjects.GetProjectAsync(owner.Id, projectId);
    Check(found.Succeeded && beforeConfirmation!.Participants.Count == participantCount && confirmed.Succeeded
        && afterConfirmation!.Participants.Count == participantCount + 1
        && afterConfirmation.Participants.Any(x => x.UserId == owner.Id && x.IsResponsible && x.LinkConfirmed)
        && afterConfirmation.Participants.Any(x => x.UserId == linked.Id && !x.IsResponsible && x.LinkConfirmed),
        "PARTICIPANTE APROBADO/RESPONSABLE", "Solo confirmación agrega al alumno aprobado y el responsable principal se conserva");
    var linkedList = await studentProjects.GetLinkedProjectsAsync(linked.Id);
    var ownerLinkedList = await studentProjects.GetLinkedProjectsAsync(owner.Id);
    var linkedDetails = await studentProjects.GetLinkedProjectAsync(linked.Id, projectId);
    Check(linkedList.Count(x => x.Id == projectId && x.IsLinkedParticipant) == 1
        && ownerLinkedList.All(x => x.Id != projectId)
        && (await studentProjects.GetProjectsAsync(owner.Id)).Count(x => x.Id == projectId) == 1,
        "PROYECTO VINCULADO LISTA", "Participante ve el proyecto una vez; el responsable lo conserva como propio, sin duplicado");
    Check(linkedDetails is { IsResponsible: false } && linkedDetails.Id == projectId
        && await studentProjects.GetLinkedProjectAsync(other.Id, projectId) is null
        && !(await studentProjects.UpdateDraftAsync(linked.Id, projectId, ValidDraft("Intento de edición no permitido", subjectId, projectTypeId, schoolCycleId))).Succeeded
        && !(await studentProjects.SubmitAsync(linked.Id, linked.FullName, projectId)).Succeeded
        && !(await studentProjects.RemoveParticipantAsync(linked.Id, linked.FullName, projectId, linkedDetails.Participants.First(x => x.UserId == linked.Id).Id)).Succeeded,
        "PROYECTO VINCULADO SOLO LECTURA", "Participante confirmado puede consultar, pero no editar, enviar ni administrar integrantes; usuario ajeno no puede abrirlo");

    var projectController = new PortalProyectos.Web.Areas.Student.Controllers.ProjectsController(
        studentProjects,
        mainScope.ServiceProvider.GetRequiredService<IProjectCatalogService>(),
        documents,
        mainScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>())
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, linked.Id)], "Functional"))
            }
        }
    };
    var allScope = (ViewResult)await projectController.Index(null, "all", CancellationToken.None);
    var allScopeModel = (PortalProyectos.Web.Models.Student.StudentProjectsViewModel)allScope.Model!;
    var ownScope = (ViewResult)await projectController.Index(null, "own", CancellationToken.None);
    var ownScopeModel = (PortalProyectos.Web.Models.Student.StudentProjectsViewModel)ownScope.Model!;
    var linkedScope = (ViewResult)await projectController.Index(null, "linked", CancellationToken.None);
    var linkedScopeModel = (PortalProyectos.Web.Models.Student.StudentProjectsViewModel)linkedScope.Model!;
    projectController.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, owner.Id)], "Functional"));
    var ownerAllScope = (ViewResult)await projectController.Index(null, "all", CancellationToken.None);
    var ownerAllScopeModel = (PortalProyectos.Web.Models.Student.StudentProjectsViewModel)ownerAllScope.Model!;
    var ownerScope = (ViewResult)await projectController.Index(null, "own", CancellationToken.None);
    var ownerScopeModel = (PortalProyectos.Web.Models.Student.StudentProjectsViewModel)ownerScope.Model!;
    var ownerLinkedScope = (ViewResult)await projectController.Index(null, "linked", CancellationToken.None);
    var ownerLinkedScopeModel = (PortalProyectos.Web.Models.Student.StudentProjectsViewModel)ownerLinkedScope.Model!;
    Check(allScopeModel.Projects.Count(x => x.Id == projectId) == 1
        && ownScopeModel.Projects.All(x => !x.IsLinkedParticipant)
        && linkedScopeModel.Projects.Count(x => x.Id == projectId && x.IsLinkedParticipant) == 1
        && ownerAllScopeModel.Projects.Count(x => x.Id == projectId && !x.IsLinkedParticipant) == 1
        && ownerScopeModel.Projects.Count(x => x.Id == projectId && !x.IsLinkedParticipant) == 1
        && ownerLinkedScopeModel.Projects.All(x => x.IsLinkedParticipant),
        "FILTROS PROPIOS/VINCULADOS", "La vista distingue Todos, Propios y Como participante, sin duplicar proyectos vinculados");

    Check((await studentProjects.UpdateDraftAsync(owner.Id, projectId, validDraft)).Succeeded, "PREPARACIÓN", "Datos completos guardados");
    var submitted = await studentProjects.SubmitAsync(owner.Id, owner.FullName, projectId);
    var submittedDetails = await studentProjects.GetProjectAsync(owner.Id, projectId);
    Check(submitted.Succeeded && submittedDetails!.Status == ProjectAdministrativeStatus.Submitted, "CASO 6", "Proyecto cambia a Enviado");

    var taken = await reviews.TakeReviewAsync(projectId, adminOne.Id, adminOne.FullName);
    var underReview = await reviews.GetProjectAsync(projectId);
    Check(taken.Succeeded && underReview!.Status == ProjectAdministrativeStatus.UnderReview && underReview.AssignedAdministratorId == adminOne.Id, "CASO 7", "Administrador toma y queda asignado");

    var editDuringReview = await studentProjects.UpdateDraftAsync(owner.Id, projectId, validDraft);
    Check(!editDuringReview.Succeeded, "CASO 8", "Edición bloqueada durante revisión");

    var changes = await reviews.RequestChangesAsync(projectId, adminOne.Id, adminOne.FullName,
        new ProjectDecisionData("Amplía la justificación técnica y precisa el alcance.", []));
    var changesDetails = await studentProjects.GetProjectAsync(owner.Id, projectId);
    Check(changes.Succeeded && changesDetails!.Status == ProjectAdministrativeStatus.ChangesRequested && changesDetails.VersionNumber == 2
        && changesDetails.Reviews.Any(x => x.Notes!.Contains("justificación")), "CASO 9", "Correcciones visibles y nueva versión editable");
    mainScope.ServiceProvider.GetRequiredService<PortalDbContext>().ChangeTracker.Clear();

    var correctedDraft = validDraft with { ProposedSolution = LongText("La solución corregida incorpora validaciones adicionales, trazabilidad y una descripción precisa del alcance técnico") };
    var corrected = await studentProjects.UpdateDraftAsync(owner.Id, projectId, correctedDraft);
    var resubmitted = await studentProjects.SubmitAsync(owner.Id, owner.FullName, projectId);
    var resubmittedDetails = await studentProjects.GetProjectAsync(owner.Id, projectId);
    Check(corrected.Succeeded && resubmitted.Succeeded && resubmittedDetails!.Status == ProjectAdministrativeStatus.Resubmitted
        && resubmittedDetails.Reviews.Count == 2, "CASO 10", "Corrección reenviada como revisión nueva");

    await reviews.TakeReviewAsync(projectId, adminOne.Id, adminOne.FullName);
    var approvalDb = mainScope.ServiceProvider.GetRequiredService<PortalDbContext>();
    var notificationsBeforeApproval = await approvalDb.Notifications.CountAsync();
    var approvalVersionId = await approvalDb.Projects.Where(x => x.Id == projectId).Select(x => x.CurrentVersionId).SingleAsync();
    await approvalDb.ProjectVersions.Where(x => x.Id == approvalVersionId)
        .ExecuteUpdateAsync(update => update.SetProperty(x => x.DevelopmentStatus, ProjectDevelopmentStatus.Completed));
    var approved = await reviews.ApproveAsync(projectId, adminOne.Id, adminOne.FullName, "Contenido validado.");
    var approvedDetails = await studentProjects.GetProjectAsync(owner.Id, projectId);
    Check(approved.Succeeded && approvedDetails!.Status == ProjectAdministrativeStatus.Approved, "CASO 11", "Proyecto aprobado sin publicación automática");

    Check(approvedDetails!.DevelopmentStatus == ProjectDevelopmentStatus.InProgress
        && await approvalDb.Notifications.CountAsync() == notificationsBeforeApproval + 1,
        "DESARROLLO APROBACIÓN", "Aprobar inicializa En proceso en la operación y genera solo el aviso existente");
    var rejectedProjectId = await CreateValidSubmittedProjectAsync(studentProjects, owner, subjectId, projectTypeId, schoolCycleId, "Proyecto destinado a prueba de rechazo");
    await reviews.TakeReviewAsync(rejectedProjectId, adminOne.Id, adminOne.FullName);
    var rejected = await reviews.RejectAsync(rejectedProjectId, adminOne.Id, adminOne.FullName, null);
    var rejectedDetails = await studentProjects.GetProjectAsync(owner.Id, rejectedProjectId);
    Check(rejected.Succeeded && rejectedDetails!.Status == ProjectAdministrativeStatus.Rejected, "CASO 12", "Proyecto rechazado con motivo opcional");

    var foreignRead = await studentProjects.GetProjectAsync(other.Id, projectId);
    var foreignEdit = await studentProjects.UpdateDraftAsync(other.Id, projectId, validDraft);
    Check(foreignRead is null && !foreignEdit.Succeeded, "CASO 13", "Otro alumno no puede leer ni editar por ID");

    var raceProjectId = await CreateValidSubmittedProjectAsync(studentProjects, owner, subjectId, projectTypeId, schoolCycleId, "Proyecto para validar concurrencia administrativa");
    await using var adminScopeOne = provider.CreateAsyncScope();
    await using var adminScopeTwo = provider.CreateAsyncScope();
    var reviewOne = adminScopeOne.ServiceProvider.GetRequiredService<IProjectReviewService>();
    var reviewTwo = adminScopeTwo.ServiceProvider.GetRequiredService<IProjectReviewService>();
    var raceResults = await Task.WhenAll(
        reviewOne.TakeReviewAsync(raceProjectId, adminOne.Id, adminOne.FullName),
        reviewTwo.TakeReviewAsync(raceProjectId, adminTwo.Id, adminTwo.FullName));
    var raceDetails = await reviews.GetProjectAsync(raceProjectId);
    Check(raceResults.Count(x => x.Succeeded) == 1 && raceDetails!.Status == ProjectAdministrativeStatus.UnderReview
        && (raceDetails.AssignedAdministratorId == adminOne.Id || raceDetails.AssignedAdministratorId == adminTwo.Id), "CASO 14", "Actualización atómica permite un solo administrador asignado");

    var accountService = mainScope.ServiceProvider.GetRequiredService<IStudentAccountService>();
    var registration = await accountService.RegisterAsync(new RegisterStudentRequest("Cuenta de Fase Tres", "NEWPHASE3001", "fase3@test.local", "Cuenta!Fase3001", careerId, gradeId, groupId));
    var notificationService = mainScope.ServiceProvider.GetRequiredService<INotificationService>();
    var adminNotices = await notificationService.GetRecentAsync(adminOne.Id, 20);
    Check(registration.Succeeded && adminNotices.Any(x => x.Type == NotificationType.AccountPending && x.Message.Contains("NEWPHASE3001")), "CASO 7", "Registro crea aviso persistente para administración");
    Check(testPublisher.Deliveries.Any(x => x.UserId == adminOne.Id && x.Notification.Type == NotificationType.AccountPending), "CASO 8", "Publicador SignalR recibe el aviso administrativo");

    var accountChanges = mainScope.ServiceProvider.GetRequiredService<IAccountAdministrationService>();
    var pendingNoticeOne = (await notificationService.CreateForUserAsync(adminOne.Id, new NotificationDraft(NotificationType.AccountPending,
        "Cuenta pendiente", "Cuenta por revisar.", "User", pending.Id, "/Admin/Users", "functional.pending.shared.one"))).Single();
    var pendingNoticeTwo = (await notificationService.CreateForUserAsync(adminTwo.Id, new NotificationDraft(NotificationType.AccountPending,
        "Cuenta pendiente", "Cuenta por revisar.", "User", pending.Id, "/Admin/Users", "functional.pending.shared.two"))).Single();
    var accountApproved = await accountChanges.ChangeStatusAsync(pending.Id, AccountStatus.Approved, null, adminOne.Id, adminOne.FullName);
    var resolvedPendingForFirstAdmin = await notificationService.GetByIdAsync(adminOne.Id, pendingNoticeOne);
    var resolvedPendingForSecondAdmin = await notificationService.GetByIdAsync(adminTwo.Id, pendingNoticeTwo);
    var accountBlocked = await accountChanges.ChangeStatusAsync(pending.Id, AccountStatus.Blocked, "Verificación pendiente", adminOne.Id, adminOne.FullName);
    var accountUnblocked = await accountChanges.UnblockAsync(pending.Id, adminOne.Id, adminOne.FullName);
    var accountNotices = await notificationService.GetRecentAsync(pending.Id, 20);
    Check(accountApproved.Succeeded && accountBlocked.Succeeded && accountUnblocked.Succeeded
        && resolvedPendingForFirstAdmin is { IsRead: true } && resolvedPendingForSecondAdmin is { IsRead: true }
        && testPublisher.UnreadUpdates.Any(x => x.UserId == adminTwo.Id)
        && (await notificationService.GetRecentAsync(adminTwo.Id, 50)).All(x => x.Id != pendingNoticeTwo)
        && accountNotices.Any(x => x.Type == NotificationType.AccountApproved)
        && accountNotices.Any(x => x.Type == NotificationType.AccountBlocked && x.Message.Contains("Verificación"))
        && accountNotices.Any(x => x.Type == NotificationType.AccountUnblocked), "CASO 8b", "Aprobación, bloqueo y desbloqueo notifican al alumno");

    var ownerNotices = await notificationService.GetRecentAsync(owner.Id, 30);
    var adminHistory = await notificationService.GetPageAsync(adminOne.Id, unreadOnly: false, page: 1, pageSize: 50);
    Check(adminHistory.Items.Any(x => x.Type == NotificationType.ProjectSubmitted && x.EntityId == projectId.ToString() && x.IsRead)
        && (await notificationService.GetRecentAsync(adminOne.Id, 50)).All(x => x.Type != NotificationType.ProjectSubmitted || x.EntityId != projectId.ToString()),
        "AVISO COMPARTIDO RESUELTO", "Tomar una revisión marca como leídos los pendientes compartidos, conserva el historial y actualiza el dropdown");
    Check(ownerNotices.Any(x => x.Type == NotificationType.ProjectAssigned), "CASO 10", "Tomar revisión avisa al responsable");
    Check(ownerNotices.Any(x => x.Type == NotificationType.ProjectChangesRequested), "CASO 11", "Solicitar correcciones avisa al responsable");
    Check(ownerNotices.Any(x => x.Type == NotificationType.ProjectApproved), "CASO 12", "Aprobación avisa al responsable");
    Check(ownerNotices.Any(x => x.Type == NotificationType.ProjectRejected && x.EntityId == rejectedProjectId.ToString()), "CASO 13", "Rechazo queda disponible aunque el alumno esté desconectado");

    var adminOwnNotice = adminNotices.First(x => !x.IsRead);
    Check(!await notificationService.MarkAsReadAsync(adminTwo.Id, adminOwnNotice.Id), "CASO 14", "Una cuenta no puede marcar como leída la notificación ajena");
    Check(await notificationService.MarkAsReadAsync(adminOne.Id, adminOwnNotice.Id), "CASO 15", "El destinatario puede marcar su aviso como leído");
    var openNoticeIds = await notificationService.CreateForUserAsync(adminOne.Id, new NotificationDraft(NotificationType.ProjectApproved,
        "Abrir marca como leída", "Prueba de apertura segura.", "Project", projectId.ToString(), $"/Student/Projects/Details/{projectId}", "functional.open.read"));
    var foreignNoticeIds = await notificationService.CreateForUserAsync(adminTwo.Id, new NotificationDraft(NotificationType.ProjectRejected,
        "Aviso de otra cuenta", "No debe aparecer en recientes ajenos.", null, null, "/Notifications", "functional.recent.foreign"));
    var recentController = CreateNotificationController(notificationService, adminOne.Id);
    var recentResult = (OkObjectResult)await recentController.Recent(CancellationToken.None);
    var recentPayload = (PortalProyectos.Web.Models.Notifications.NotificationRecentResponse)recentResult.Value!;
    var recentItems = recentPayload.Items.ToArray();
    var jsonRecent = JsonSerializer.Serialize(recentPayload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    var beforeOpenUnread = await notificationService.GetUnreadCountAsync(adminOne.Id);
    var openController = CreateNotificationController(notificationService, adminOne.Id);
    var openedNotice = await openController.Open(openNoticeIds.Single(), CancellationToken.None);
    var afterOpenUnread = await notificationService.GetUnreadCountAsync(adminOne.Id);
    var foreignOpen = await CreateNotificationController(notificationService, adminTwo.Id).Open(openNoticeIds.Single(), CancellationToken.None);
    var alreadyReadOpen = await CreateNotificationController(notificationService, adminOne.Id).Open(openNoticeIds.Single(), CancellationToken.None);
    Check(recentItems.Any(x => x.Id == openNoticeIds.Single() && x.Type == nameof(NotificationType.ProjectApproved))
        && recentItems.All(x => x.Id != foreignNoticeIds.Single())
        && recentItems.All(x => !x.IsRead) && recentPayload.UnreadCount == beforeOpenUnread
        && jsonRecent.Contains("\"type\":\"ProjectApproved\"", StringComparison.Ordinal)
        && jsonRecent.Contains("\"createdAtUtc\"", StringComparison.Ordinal),
        "NOTIFICACIONES DROPDOWN DTO", "Endpoint entrega recientes propios en JSON con tipo string y casing web esperado por el cliente");
    var manuallyReadId = (await notificationService.CreateForUserAsync(adminOne.Id, new NotificationDraft(NotificationType.AccountPending,
        "Lectura manual", "Aviso para descartar sin abrir.", "User", pending.Id, "/Notifications", "functional.manual.read"))).Single();
    var beforeManualRead = await notificationService.GetUnreadCountAsync(adminOne.Id);
    var manualRead = await notificationService.MarkAsReadAsync(adminOne.Id, manuallyReadId);
    Check(manualRead && await notificationService.GetUnreadCountAsync(adminOne.Id) == beforeManualRead - 1
        && (await notificationService.GetRecentAsync(adminOne.Id, 50)).All(x => x.Id != manuallyReadId),
        "NOTIFICACIONES MARCADO MANUAL", "Marcar como leída reduce contador y retira el aviso de recientes sin borrarlo del historial");
    Check(openedNotice is LocalRedirectResult && beforeOpenUnread == afterOpenUnread + 1
        && (await notificationService.GetByIdAsync(adminOne.Id, openNoticeIds.Single()))!.IsRead
        && foreignOpen is NotFoundResult && alreadyReadOpen is LocalRedirectResult,
        "NOTIFICACIONES ABRIR", "Abrir marca persistente como leída antes de redirigir; contador baja, ajena devuelve 404 y abrir leída sigue válido");

    testPublisher.Fail = true;
    var offlineIds = await notificationService.CreateForUserAsync(owner.Id, new NotificationDraft(NotificationType.ProjectApproved,
        "Aviso sin conexión", "Persistencia independiente del transporte.", "Project", projectId.ToString(), $"/Student/Projects/Details/{projectId}", "functional.offline.persist"));
    await notificationService.PublishAsync(offlineIds);
    testPublisher.Fail = false;
    Check(offlineIds.Count == 1 && (await notificationService.GetByIdAsync(owner.Id, offlineIds[0])) is not null, "CASO 15b", "Falla el transporte sin perder el aviso persistente");
    var dedupeDraft = new NotificationDraft(NotificationType.AccountPending, "Evento repetido", "No se debe duplicar.", null, null, null, "functional.dedupe");
    var firstDedupe = await notificationService.CreateForUserAsync(owner.Id, dedupeDraft);
    var retryDedupe = await notificationService.CreateForUserAsync(owner.Id, dedupeDraft);
    Check(firstDedupe.Count == 1 && retryDedupe.Count == 0, "CASO 15c", "Reintentar un evento con clave estable no duplica la notificación");

    var dbContext = mainScope.ServiceProvider.GetRequiredService<PortalDbContext>();
    var pdfProject = await studentProjects.CreateDraftAsync(owner.Id, owner.FullName);
    Check((await studentProjects.UpdateDraftAsync(owner.Id, pdfProject.Value, ValidDraft("Proyecto PDF funcional de prueba", subjectId, projectTypeId, schoolCycleId))).Succeeded,
        "PREPARACIÓN PDF", "Guarda los datos básicos antes de asociar participantes o PDF");
    var newProjectStatus = (await studentProjects.GetProjectAsync(owner.Id, pdfProject.Value))!.DevelopmentStatus;
    var editableStatusChanged = await studentProjects.UpdateDraftAsync(owner.Id, pdfProject.Value,
        ValidDraft("Proyecto PDF funcional de prueba", subjectId, projectTypeId, schoolCycleId) with { DevelopmentStatus = ProjectDevelopmentStatus.Completed });
    var completedDraftStatus = (await studentProjects.GetProjectAsync(owner.Id, pdfProject.Value))!.DevelopmentStatus;
    var editableStatusRestored = await studentProjects.UpdateDraftAsync(owner.Id, pdfProject.Value,
        ValidDraft("Proyecto PDF funcional de prueba", subjectId, projectTypeId, schoolCycleId) with { DevelopmentStatus = ProjectDevelopmentStatus.InProgress });
    Check(newProjectStatus == ProjectDevelopmentStatus.InProgress && editableStatusChanged.Succeeded
        && completedDraftStatus == ProjectDevelopmentStatus.InProgress && editableStatusRestored.Succeeded,
        "ESTADO DESARROLLO BORRADOR", "El valor interno inicial no puede cambiarse mediante datos manipulados del alumno");
    var linkToPdfProject = await studentProjects.AddLinkedParticipantAsync(owner.Id, owner.FullName, pdfProject.Value, linked.Id);
    var pdfBytes = System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\nDocumento funcional de prueba\n%%EOF");
    var upload = await documents.UploadAsync(owner.Id, owner.FullName, pdfProject.Value, "avance.pdf", "application/pdf", pdfBytes.Length, new MemoryStream(pdfBytes));
    var currentDocument = await documents.GetForProjectOwnerAsync(owner.Id, pdfProject.Value);
    var currentMedia = await dbContext.MediaFiles.SingleAsync(x => x.Id == currentDocument!.Id);
    Check(linkToPdfProject.Succeeded && upload.Succeeded && File.Exists(Path.Combine(storageRoot, currentMedia.RelativePath.Replace('/', Path.DirectorySeparatorChar))), "CASO 1", "PDF válido crea archivo físico y metadatos SQLite");

    var invalidExtension = await documents.UploadAsync(owner.Id, owner.FullName, pdfProject.Value, "documento.txt", "application/pdf", pdfBytes.Length, new MemoryStream(pdfBytes));
    var invalidMime = await documents.UploadAsync(owner.Id, owner.FullName, pdfProject.Value, "documento.pdf", "text/plain", pdfBytes.Length, new MemoryStream(pdfBytes));
    var invalidSignatureBytes = System.Text.Encoding.ASCII.GetBytes("NOT A PDF FILE");
    var invalidSignature = await documents.UploadAsync(owner.Id, owner.FullName, pdfProject.Value, "fake.pdf", "application/pdf", invalidSignatureBytes.Length, new MemoryStream(invalidSignatureBytes));
    Check(!invalidExtension.Succeeded && !invalidMime.Succeeded && !invalidSignature.Succeeded, "CASO 2", "Extensión, MIME o firma falsa se rechazan");
    var oversized = await documents.UploadAsync(owner.Id, owner.FullName, pdfProject.Value, "grande.pdf", "application/pdf", 10 * 1024 * 1024 + 1, new MemoryStream(pdfBytes));
    Check(!oversized.Succeeded, "CASO 3", "PDF mayor de 10 MB se rechaza antes de guardarlo");
    Check(await documents.GetForProjectOwnerAsync(other.Id, pdfProject.Value) is null
        && await documents.OpenForProjectOwnerAsync(other.Id, pdfProject.Value) is null, "CASO 4", "Otro alumno no puede obtener ni descargar el PDF");

    var oldPath = currentMedia.RelativePath;
    var replacementBytes = System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\nSegunda versión del documento\n%%EOF");
    var replaced = await documents.UploadAsync(owner.Id, owner.FullName, pdfProject.Value, "memoria-final.pdf", "application/pdf", replacementBytes.Length, new MemoryStream(replacementBytes));
    var oldMedia = await dbContext.MediaFiles.SingleAsync(x => x.Id == currentDocument!.Id);
    Check(replaced.Succeeded && oldMedia.IsDeleted && !File.Exists(Path.Combine(storageRoot, oldPath.Replace('/', Path.DirectorySeparatorChar))), "CASO 5", "Reemplazo guarda el nuevo PDF y retira el archivo anterior");

    var protectedSubmit = await studentProjects.UpdateDraftAsync(owner.Id, pdfProject.Value, ValidDraft("Documento protegido en revisión", subjectId, projectTypeId, schoolCycleId));
    var protectedSend = await studentProjects.SubmitAsync(owner.Id, owner.FullName, pdfProject.Value);
    await reviews.TakeReviewAsync(pdfProject.Value, adminOne.Id, adminOne.FullName);
    var uploadDuringReview = await documents.UploadAsync(owner.Id, owner.FullName, pdfProject.Value, "locked.pdf", "application/pdf", pdfBytes.Length, new MemoryStream(pdfBytes));
    var deleteDuringReview = await documents.DeleteAsync(owner.Id, owner.FullName, pdfProject.Value);
    var administratorDocument = await documents.GetForAdministratorAsync(adminOne.Id, pdfProject.Value);
    Check(protectedSubmit.Succeeded && protectedSend.Succeeded && !uploadDuringReview.Succeeded && !deleteDuringReview.Succeeded
        && administratorDocument is not null, "CASO 6", "Alumno no puede cambiar el PDF durante revisión y administración puede consultarlo");
    await reviews.RequestChangesAsync(pdfProject.Value, adminOne.Id, adminOne.FullName, new ProjectDecisionData("Adjunta una nueva evidencia del avance.", []));
    var originalVersionDocument = await dbContext.MediaFiles.AsNoTracking().SingleAsync(x => x.ProjectVersionId == currentMedia.ProjectVersionId && !x.IsDeleted);
    var versionTwoDocument = await documents.GetForProjectOwnerAsync(owner.Id, pdfProject.Value);
    Check(originalVersionDocument.Id != currentDocument!.Id && versionTwoDocument is null, "CASO 6b", "Versión nueva no sobrescribe documentación histórica");

    dbContext.ChangeTracker.Clear();
    var approvedReplacementData = ValidDraft("Documento aprobado con evidencia posterior", subjectId, projectTypeId, schoolCycleId);
    var secondSave = await studentProjects.UpdateDraftAsync(owner.Id, pdfProject.Value, approvedReplacementData);
    var secondSubmit = await studentProjects.SubmitAsync(owner.Id, owner.FullName, pdfProject.Value);
    var resubmittedUpload = await documents.UploadAsync(owner.Id, owner.FullName, pdfProject.Value, "bloqueado-reenviado.pdf", "application/pdf", pdfBytes.Length, new MemoryStream(pdfBytes));
    dbContext.ChangeTracker.Clear();
    var secondTake = await reviews.TakeReviewAsync(pdfProject.Value, adminOne.Id, adminOne.FullName);
    var underReviewUpload = await documents.UploadAsync(owner.Id, owner.FullName, pdfProject.Value, "bloqueado-revision.pdf", "application/pdf", pdfBytes.Length, new MemoryStream(pdfBytes));
    dbContext.ChangeTracker.Clear();
    var approvePdfProject = await reviews.ApproveAsync(pdfProject.Value, adminOne.Id, adminOne.FullName, null);
    dbContext.ChangeTracker.Clear();
    var academicSnapshot = await studentProjects.GetProjectAsync(owner.Id, pdfProject.Value);
    var versionCountBeforeDocumentUpdates = await dbContext.ProjectVersions.CountAsync(x => x.ProjectId == pdfProject.Value);
    var addApprovedPdf = await documents.UploadAsync(owner.Id, owner.FullName, pdfProject.Value, "evidencia-aprobada.pdf", "application/pdf", pdfBytes.Length, new MemoryStream(pdfBytes));
    var approvedPdf = await documents.GetForProjectOwnerAsync(owner.Id, pdfProject.Value);
    var linkedApprovedPdf = await documents.GetForLinkedParticipantAsync(linked.Id, pdfProject.Value);
    var linkedPdfStream = await documents.OpenForLinkedParticipantAsync(linked.Id, pdfProject.Value);
    var linkedCannotReplacePdf = await documents.UploadAsync(linked.Id, linked.FullName, pdfProject.Value, "ajeno.pdf", "application/pdf", pdfBytes.Length, new MemoryStream(pdfBytes));
    var linkedCannotDeletePdf = await documents.DeleteAsync(linked.Id, linked.FullName, pdfProject.Value);
    linkedPdfStream?.Content.Dispose();
    Check(approvePdfProject.Succeeded && addApprovedPdf.Succeeded && linkedApprovedPdf is not null && linkedPdfStream is not null
        && !linkedCannotReplacePdf.Succeeded && !linkedCannotDeletePdf.Succeeded,
        "PDF PARTICIPANTE", "Participante vinculado consulta/descarga el PDF vigente aprobado sin permiso de reemplazo o eliminación");
    var approvedPdfEntity = await dbContext.MediaFiles.AsNoTracking().SingleAsync(x => x.Id == approvedPdf!.Id);
    var approvedPath = approvedPdfEntity.RelativePath;
    var invalidApprovedPdf = await documents.UploadAsync(owner.Id, owner.FullName, pdfProject.Value, "invalido.pdf", "application/pdf", invalidSignatureBytes.Length, new MemoryStream(invalidSignatureBytes));
    testFileStorage.FailNextSave = true;
    var storageFailure = await documents.UploadAsync(owner.Id, owner.FullName, pdfProject.Value, "fallo-storage.pdf", "application/pdf", pdfBytes.Length, new MemoryStream(pdfBytes));
    Check(!invalidApprovedPdf.Succeeded && !storageFailure.Succeeded
        && await documents.GetForProjectOwnerAsync(owner.Id, pdfProject.Value) is { } retainedAfterFailedReplace
        && retainedAfterFailedReplace.Id == approvedPdf!.Id
        && File.Exists(Path.Combine(storageRoot, approvedPath.Replace('/', Path.DirectorySeparatorChar))), "PDF APROBADO FALLAS", "PDF aprobado conserva el vigente al fallar validación o storage");

    var failureTrigger = string.Concat("CREATE TRIGGER fail_project_pdf_insert BEFORE INSERT ON MediaFiles WHEN NEW.ProjectId = ",
        pdfProject.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), " AND NEW.FileType = 'ProjectDocument'",
        " BEGIN SELECT RAISE(ABORT, 'functional replacement failure'); END;");
    await dbContext.Database.ExecuteSqlRawAsync(failureTrigger);
    var databaseFailure = await documents.UploadAsync(owner.Id, owner.FullName, pdfProject.Value, "fallo-db.pdf", "application/pdf", replacementBytes.Length, new MemoryStream(replacementBytes));
    await dbContext.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_project_pdf_insert;");
    dbContext.ChangeTracker.Clear();
    var retainedAfterDbFailure = await documents.GetForProjectOwnerAsync(owner.Id, pdfProject.Value);
    var activeApprovedFiles = await dbContext.MediaFiles.AsNoTracking().CountAsync(x => x.ProjectId == pdfProject.Value && x.ProjectVersionId == academicSnapshot!.VersionId && x.FileType == StoredFileType.ProjectDocument && !x.IsDeleted);
    var replaceApprovedPdf = await documents.UploadAsync(owner.Id, owner.FullName, pdfProject.Value, "evidencia-actualizada.pdf", "application/pdf", replacementBytes.Length, new MemoryStream(replacementBytes));
    var secondRetainedAfterReplace = await documents.GetForProjectOwnerAsync(owner.Id, pdfProject.Value);
    var changedAcademicSnapshot = await studentProjects.GetProjectAsync(owner.Id, pdfProject.Value);
    var postApprovalAudits = await dbContext.AuditLogs.AsNoTracking().Where(x => x.EntityId == pdfProject.Value.ToString()).Select(x => x.Action).ToListAsync();
    Check(secondSave.Succeeded && secondSubmit.Succeeded, "PDF PREPARACIÓN REENVÍO", $"Guardar={secondSave.Succeeded} ({secondSave.Message}); enviar={secondSubmit.Succeeded} ({secondSubmit.Message})");
    Check(!resubmittedUpload.Succeeded && secondTake.Succeeded && !underReviewUpload.Succeeded && approvePdfProject.Succeeded,
        "PDF ESTADOS", $"Reenviado bloqueado={ !resubmittedUpload.Succeeded }, toma={secondTake.Succeeded}, revisión bloqueada={ !underReviewUpload.Succeeded }, aprobación={approvePdfProject.Succeeded }");
    Check(academicSnapshot?.Status == ProjectAdministrativeStatus.Approved && addApprovedPdf.Succeeded
        && retainedAfterDbFailure?.Id == approvedPdf!.Id && activeApprovedFiles == 1 && !databaseFailure.Succeeded,
        "PDF TRANSACCIÓN", $"Estado={academicSnapshot?.Status}; agregar={addApprovedPdf.Succeeded}; retención={retainedAfterDbFailure?.Id == approvedPdf!.Id}; activos={activeApprovedFiles}; falloDB={!databaseFailure.Succeeded}");
    Check(!resubmittedUpload.Succeeded && secondTake.Succeeded && !underReviewUpload.Succeeded && approvePdfProject.Succeeded
        && academicSnapshot?.Status == ProjectAdministrativeStatus.Approved && addApprovedPdf.Succeeded
        && retainedAfterDbFailure?.Id == approvedPdf!.Id && activeApprovedFiles == 1 && !databaseFailure.Succeeded,
        "PDF APROBADO 1/2", "Responsable puede adjuntar a versión aprobada; Submitted, Resubmitted y UnderReview siguen bloqueados y un fallo SQLite conserva el PDF vigente");
    Check(replaceApprovedPdf.Succeeded && secondRetainedAfterReplace?.Id != approvedPdf!.Id
        && await dbContext.MediaFiles.AsNoTracking().CountAsync(x => x.ProjectId == pdfProject.Value && x.ProjectVersionId == academicSnapshot!.VersionId && x.FileType == StoredFileType.ProjectDocument && !x.IsDeleted) == 1,
        "PDF APROBADO 3", "Reemplazo aprobado deja exactamente un documento activo");
    Check(await documents.UploadAsync(other.Id, other.FullName, pdfProject.Value, "ajeno.pdf", "application/pdf", pdfBytes.Length, new MemoryStream(pdfBytes)) is { Succeeded: false }
        && changedAcademicSnapshot?.VersionId == academicSnapshot?.VersionId
        && changedAcademicSnapshot?.VersionNumber == academicSnapshot?.VersionNumber
        && changedAcademicSnapshot?.Name == academicSnapshot?.Name
        && changedAcademicSnapshot?.ProblemStatement == academicSnapshot?.ProblemStatement
        && changedAcademicSnapshot?.ProposedSolution == academicSnapshot?.ProposedSolution
        && await dbContext.ProjectVersions.CountAsync(x => x.ProjectId == pdfProject.Value) == versionCountBeforeDocumentUpdates
        && postApprovalAudits.Contains("Archivo.PDFAgregadoPostAprobacion") && postApprovalAudits.Contains("Archivo.PDFReemplazadoPostAprobacion"),
        "PDF APROBADO 4/5/6", "Solo responsable puede actualizar; contenido/versionado académico no cambia y ambas operaciones quedan auditadas");

    var disposableDraft = await studentProjects.CreateDraftAsync(owner.Id, owner.FullName);
    var savedDisposableBasics = await studentProjects.UpdateDraftAsync(owner.Id, disposableDraft.Value,
        ValidDraft("Borrador con documentación para eliminar", subjectId, projectTypeId, schoolCycleId));
    var disposableUpload = await documents.UploadAsync(owner.Id, owner.FullName, disposableDraft.Value, "borrar.pdf", "application/pdf", pdfBytes.Length, new MemoryStream(pdfBytes));
    var deletedDocument = await documents.DeleteAsync(owner.Id, owner.FullName, disposableDraft.Value);
    var deletedMedia = await dbContext.MediaFiles.AsNoTracking().Where(x => x.ProjectId == disposableDraft.Value).ToListAsync();
    var deletedDraft = await studentProjects.DeleteDraftAsync(owner.Id, owner.FullName, disposableDraft.Value);
    Check(savedDisposableBasics.Succeeded && disposableUpload.Succeeded && deletedDocument.Succeeded && deletedMedia.Single().IsDeleted && deletedDraft.Succeeded
        && !File.Exists(Path.Combine(storageRoot, deletedMedia.Single().RelativePath.Replace('/', Path.DirectorySeparatorChar))), "CASO 16", "Eliminar PDF marca metadatos, borra archivo y permite borrar borrador vacío");

    await notificationService.CreateForUserAsync(adminOne.Id, new NotificationDraft(NotificationType.AccountPending,
        "Marcar todas", "Aviso pendiente para verificar lectura masiva.", null, null, "/Notifications", "functional.mark-all"));
    var markedCount = await notificationService.MarkAllAsReadAsync(adminOne.Id);
    Check(markedCount >= 1 && await notificationService.GetUnreadCountAsync(adminOne.Id) == 0, "CASO 15d", "Marcar todas como leídas actualiza el contador persistente");

    Check(await VerifyLiveSignalRAsync(), "CASO 8c", "SignalR entrega en vivo sólo al usuario destinatario autenticado");

    dbContext.ChangeTracker.Clear();
    var publications = mainScope.ServiceProvider.GetRequiredService<IProjectPublicationService>();
    var beforePublish = await publications.GetPublicPageAsync(new PublicProjectFilter(null, null, null, null, null, null, null), 1, owner.Id);
    Check(beforePublish.Items.All(x => x.Id != projectId), "CASO 1", "Proyecto aprobado no se muestra antes de publicarlo");
    var publishDetails = await publications.GetAdminDetailsAsync(projectId);
    var publishedVersionId = publishDetails!.ApprovedVersions.First().Id;
    var publishResult = await publications.PublishAsync(projectId, publishedVersionId, adminOne.Id, adminOne.FullName, true);
    var afterPublish = await publications.GetPublicPageAsync(new PublicProjectFilter(null, null, null, null, null, null, null), 1, owner.Id);
    Check(publishResult.Succeeded && afterPublish.Items.Any(x => x.Id == projectId && x.IsFeatured), "CASO 2/9", "Publicación explícita muestra el proyecto y prioriza destacados");

    var invalidImage = await publications.UploadImageAsync(projectId, adminOne.Id, adminOne.FullName, "no.jpg", "image/jpeg", 8, new MemoryStream(Encoding.ASCII.GetBytes("no image")), null);
    var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/FqsAAAAASUVORK5CYII=");
    var uploadedImage = await publications.UploadImageAsync(projectId, adminOne.Id, adminOne.FullName, "portada.png", "image/png", png.Length, new MemoryStream(png), "Prototipo del proyecto");
    var adminPublication = await publications.GetAdminDetailsAsync(projectId);
    var cover = adminPublication!.Images.Single();
    var previewVersion = adminPublication.ApprovedVersions.Single(x => x.Id == publishedVersionId);
    var noCoverPreview = await publications.GetAdminDetailsAsync(pdfProject.Value);
    var openedCover = await publications.OpenPublicImageAsync(cover.Id);
    Check(!invalidImage.Succeeded && uploadedImage.Succeeded && cover.IsCover && openedCover is not null
        && previewVersion.Name == adminPublication.Name && previewVersion.Career.Length > 0 && previewVersion.Subject.Length > 0
        && noCoverPreview is not null && noCoverPreview.Images.All(x => !x.IsCover),
        "PUBLICACIÓN PREVIEW", "Modelo incluye portada vigente y datos de la versión aprobada seleccionada; sin portada el fallback queda como caso explícito");
    openedCover?.Content.Dispose();
    var adminImage = await publications.OpenAdminImageAsync(adminOne.Id, cover.Id);
    Check(adminImage is not null, "CASO 19", "La imagen administrativa se sirve con autorización y no por ruta física");
    adminImage?.Content.Dispose();

    var liveBeforeChange = await publications.GetPublicDetailsAsync(projectId, owner.Id);
    dbContext.ChangeTracker.Clear();
    var publishedProject = await dbContext.Projects.SingleAsync(x => x.Id == projectId);
    var oldVersion = await dbContext.ProjectVersions.SingleAsync(x => x.Id == publishedVersionId);
    var nextVersion = new ProjectVersion { ProjectId = projectId, VersionNumber = oldVersion.VersionNumber + 1, Name = "Versión nueva en revisión", SubjectId = oldVersion.SubjectId, ProjectTypeId = oldVersion.ProjectTypeId, SchoolCycleId = oldVersion.SchoolCycleId, DevelopmentStatus = oldVersion.DevelopmentStatus, ProblemStatement = oldVersion.ProblemStatement, ProposedSolution = oldVersion.ProposedSolution, Advisor = oldVersion.Advisor, CreatedAtUtc = DateTimeOffset.UtcNow };
    dbContext.ProjectVersions.Add(nextVersion);
    await dbContext.SaveChangesAsync();
    publishedProject.CurrentVersionId = nextVersion.Id;
    publishedProject.CurrentStatus = ProjectAdministrativeStatus.UnderReview;
    await dbContext.SaveChangesAsync();
    var duringNewReview = await publications.GetPublicDetailsAsync(projectId, owner.Id);
    Check(liveBeforeChange!.Project.VersionId == publishedVersionId && duringNewReview!.Project.VersionId == publishedVersionId, "CASO 14", "La versión pública anterior permanece visible mientras se revisa una versión nueva");
    dbContext.ProjectReviews.Add(new ProjectReview { ProjectId = projectId, ProjectVersionId = nextVersion.Id, ReviewNumber = (await dbContext.ProjectReviews.Where(x => x.ProjectId == projectId).MaxAsync(x => (int?)x.ReviewNumber) ?? 0) + 1, AdministratorId = adminOne.Id, Decision = ProjectReviewDecision.Approved, CompletedAtUtc = DateTimeOffset.UtcNow });
    publishedProject.LatestApprovedVersionId = nextVersion.Id;
    publishedProject.CurrentStatus = ProjectAdministrativeStatus.Approved;
    await dbContext.SaveChangesAsync();
    var beforeExplicitVersionPublish = await publications.GetPublicDetailsAsync(projectId, owner.Id);
    Check(beforeExplicitVersionPublish!.Project.VersionId == publishedVersionId, "CASO 15", "La aprobación de la versión nueva no cambia la publicación sin acción administrativa");
    var changedPublicVersion = await publications.PublishAsync(projectId, nextVersion.Id, adminOne.Id, adminOne.FullName, true);
    var afterExplicitVersionPublish = await publications.GetPublicDetailsAsync(projectId, owner.Id);
    Check(changedPublicVersion.Succeeded && afterExplicitVersionPublish!.Project.VersionId == nextVersion.Id, "CASO 16", "Publicar manualmente la versión nueva sustituye la referencia pública");

    dbContext.ChangeTracker.Clear();
    var participant = await dbContext.ProjectParticipants.FirstAsync(x => x.ProjectId == projectId && x.IsResponsible);
    var originalPublicParticipant = liveBeforeChange.Participants.First(x => x.Name == participant.FullName).Name;
    participant.FullName = "Nombre modificado sólo para validar snapshot";
    await dbContext.SaveChangesAsync();
    var snapshotDetail = await publications.GetPublicDetailsAsync(projectId, owner.Id);
    Check(snapshotDetail!.Participants.Any(x => x.Name == originalPublicParticipant) && snapshotDetail.Participants.All(x => x.Name != participant.FullName), "VERSIONADO", "Participantes públicos quedan congelados por versión hasta republicación explícita");

    var pendingUser = await setupScopeUser(provider, "NEWPHASE3001");
    var pendingLike = await publications.ToggleLikeAsync(pendingUser.Id, projectId);
    var secondPublishedVersion = (await publications.GetAdminDetailsAsync(pdfProject.Value))!.ApprovedVersions.First();
    var publishSecondProject = await publications.PublishAsync(pdfProject.Value, secondPublishedVersion.Id, adminOne.Id, adminOne.FullName, false);
    var firstLike = await publications.ToggleLikeAsync(owner.Id, projectId);
    var secondLike = await publications.ToggleLikeAsync(owner.Id, pdfProject.Value);
    var likeCount = (await publications.GetPublicDetailsAsync(projectId, owner.Id))!.Project.LikeCount;
    var secondLikeCount = (await publications.GetPublicDetailsAsync(pdfProject.Value, owner.Id))!.Project.LikeCount;
    var removeLike = await publications.ToggleLikeAsync(owner.Id, projectId);
    var secondAfterFirstRemoval = (await publications.GetPublicDetailsAsync(pdfProject.Value, owner.Id))!.Project.LikeCount;
    var likeUniqueIndex = dbContext.Model.FindEntityType(typeof(ProjectLike))!.GetIndexes()
        .Any(index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(ProjectLike.ProjectId), nameof(ProjectLike.UserId)]));
    var duplicateRejected = false;
    dbContext.ProjectLikes.Add(new ProjectLike { ProjectId = pdfProject.Value, UserId = owner.Id });
    try { await dbContext.SaveChangesAsync(); }
    catch (DbUpdateException) { duplicateRejected = true; dbContext.ChangeTracker.Clear(); }
    var secondAfterDuplicateAttempt = (await publications.GetPublicDetailsAsync(pdfProject.Value, owner.Id))!.Project.LikeCount;
    var removeSecondLike = await publications.ToggleLikeAsync(owner.Id, pdfProject.Value);
    var anonymousLike = await publications.ToggleLikeAsync(string.Empty, projectId);
    Check(publishSecondProject.Succeeded && !pendingLike.Succeeded && !anonymousLike.Succeeded
        && firstLike.Succeeded && firstLike.Value == true && secondLike.Succeeded && secondLike.Value == true
        && likeCount == 1 && secondLikeCount == 1 && removeLike.Value == false && secondAfterFirstRemoval == 1
        && likeUniqueIndex && duplicateRejected && secondAfterDuplicateAttempt == 1 && removeSecondLike.Value == false,
        "LIKES MULTIPROYECTO", "Un usuario conserva likes independientes por proyecto; la unicidad compuesta rechaza duplicados y retirar uno no afecta los demás");

    var hidden = await publications.SetVisibilityAsync(projectId, adminOne.Id, adminOne.FullName, false);
    var hiddenPage = await publications.GetPublicPageAsync(new PublicProjectFilter(null, null, null, null, null, null, null), 1, null);
    var republished = await publications.SetVisibilityAsync(projectId, adminOne.Id, adminOne.FullName, true);
    Check(hidden.Succeeded && hiddenPage.Items.All(x => x.Id != projectId) && republished.Succeeded, "CASO 7/8", "Ocultar retira del portal y volver a publicar lo restaura");
    var visibleImage = await publications.OpenPublicImageAsync(cover.Id);
    Check(visibleImage is not null, "CASO 19", "La imagen sólo vuelve a servirse por endpoint cuando la publicación está visible");
    visibleImage?.Content.Dispose();
    await publications.UploadImageAsync(projectId, adminOne.Id, adminOne.FullName, "galeria.png", "image/png", png.Length, new MemoryStream(png), "Galería");
    await publications.UploadImageAsync(projectId, adminOne.Id, adminOne.FullName, "eliminar.png", "image/png", png.Length, new MemoryStream(png), "Imagen adicional");
    var galleryImages = (await publications.GetAdminDetailsAsync(projectId))!.Images;
    var galleryImage = galleryImages.Single(x => x.Name == "galeria.png");
    var extraImage = galleryImages.Single(x => x.Name == "eliminar.png");
    await publications.ReorderImageAsync(projectId, cover.Id, 100, adminOne.Id, adminOne.FullName);
    await publications.ReorderImageAsync(projectId, extraImage.Id, 0, adminOne.Id, adminOne.FullName);
    var removedExtra = await publications.DeleteImageAsync(projectId, extraImage.Id, adminOne.Id, adminOne.FullName);
    dbContext.ChangeTracker.Clear();
    var afterGalleryDelete = (await publications.GetAdminDetailsAsync(projectId))!.Images;
    Check(removedExtra.Succeeded && afterGalleryDelete.Count == 2
        && afterGalleryDelete.Single(x => x.IsCover).Id == cover.Id
        && afterGalleryDelete.Single(x => !x.IsCover).Id == galleryImage.Id,
        "GALERÍA", "Eliminar una imagen reordenada conserva la portada y la galería; excluye la imagen eliminada antes de guardar");
    var deleteImage = await publications.DeleteImageAsync(projectId, cover.Id, adminOne.Id, adminOne.FullName);
    Check(deleteImage.Succeeded && !(await dbContext.MediaFiles.AsNoTracking().AnyAsync(x => x.Id == cover.Id && !x.IsDeleted)), "CASO 6", "Eliminar imagen la retira de metadatos activos y storage físico");
    dbContext.ChangeTracker.Clear();
    var afterCoverDelete = (await publications.GetAdminDetailsAsync(projectId))!.Images;
    Check(afterCoverDelete.Count == 1 && afterCoverDelete.Single().Id == galleryImage.Id && afterCoverDelete.Single().IsCover,
        "GALERÍA", "Eliminar la portada promueve una imagen restante y mantiene una sola portada");
    await publications.DeleteImageAsync(projectId, galleryImage.Id, adminOne.Id, adminOne.FullName);
    dbContext.ChangeTracker.Clear();
    Check(!(await publications.GetAdminDetailsAsync(projectId))!.Images.Any(), "GALERÍA", "Eliminar la última imagen deja la publicación sin portada ni galería");
    var publishedNotices = await notificationService.GetRecentAsync(owner.Id, 50);
    Check(publishedNotices.Any(x => x.Type == NotificationType.ProjectPublished && x.EntityId == projectId.ToString())
        && testPublisher.Deliveries.Any(x => x.UserId == owner.Id && x.Notification.Type == NotificationType.ProjectPublished)
        && publishedNotices.Any(x => x.Type == NotificationType.ProjectHidden && x.EntityId == projectId.ToString()), "CASO 20", "Publicar y ocultar generan notificaciones persistentes; publicar además entrega SignalR");
    var filtered = await publications.GetPublicPageAsync(new PublicProjectFilter("Versión nueva en revisión", careerId, subjectId, projectTypeId, ProjectDevelopmentStatus.InProgress, schoolCycleId, "likes"), 1, null);
    Check(filtered.Items.Any(x => x.Id == projectId), "CASO 17/18", "Búsqueda y filtros combinados producen el resultado esperado");

    dbContext.ChangeTracker.Clear();
    var news = mainScope.ServiceProvider.GetRequiredService<INewsPostService>();
    var studentNewsAttempt = await news.SaveDraftAsync(null, owner.Id, owner.FullName,
        new NewsPostInput("Intento de alumno", "Resumen que no debe aceptarse desde el servicio.", "Este contenido intenta comprobar que un alumno no pueda administrar el módulo editorial.", false));
    Check(!studentNewsAttempt.Succeeded, "NOTICIAS SEGURIDAD", "El servicio editorial también valida el rol, además de la autorización MVC");
    var newsDraft = await news.SaveDraftAsync(null, adminOne.Id, adminOne.FullName,
        new NewsPostInput("Aviso de prueba editorial", "Resumen editorial para validar el listado público.", "Contenido de prueba suficientemente extenso para validar el módulo editorial sin depender de internet.", false));
    Check(newsDraft.Succeeded && await news.GetPublicDetailsAsync(newsDraft.Value!) is null, "NOTICIAS 1/17", "Borrador editorial queda privado y no se obtiene por ID público");

    var pngFixture = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/FqsAAAAASUVORK5CYII=");
    var imageReject = await news.UploadImageAsync(newsDraft.Value!, adminOne.Id, adminOne.FullName, "portada.exe", "image/png", pngFixture.Length, new MemoryStream(pngFixture), null, true);
    var imageUpload = await news.UploadImageAsync(newsDraft.Value!, adminOne.Id, adminOne.FullName, "portada.png", "image/png", pngFixture.Length, new MemoryStream(pngFixture), "Portada de prueba", true);
    var mp4Fixture = new byte[] { 0, 0, 0, 20, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'i', (byte)'s', (byte)'o', (byte)'m', 0, 0, 0, 0 };
    var videoUpload = await news.UploadVideoAsync(newsDraft.Value!, adminOne.Id, adminOne.FullName, "clip.mp4", "video/mp4", mp4Fixture.Length, new MemoryStream(mp4Fixture));
    var badVideo = await news.UploadVideoAsync(newsDraft.Value!, adminOne.Id, adminOne.FullName, "clip.mp4", "video/mp4", 16, new MemoryStream(Encoding.ASCII.GetBytes("not-an-mp4-file")));
    var youtube = await news.SetExternalVideoAsync(newsDraft.Value!, "https://youtu.be/abcdefghijk", adminOne.Id, adminOne.FullName);
    var unsafeYoutube = await news.SetExternalVideoAsync(newsDraft.Value!, "javascript:alert(1)", adminOne.Id, adminOne.FullName);
    var arbitraryHost = await news.SetExternalVideoAsync(newsDraft.Value!, "https://evil.example/watch?v=abcdefghijk", adminOne.Id, adminOne.FullName);
    var externalLink = await news.AddExternalLinkAsync(newsDraft.Value!, "Información", "https://example.org/info", adminOne.Id, adminOne.FullName);
    var unsafeLink = await news.AddExternalLinkAsync(newsDraft.Value!, "Inseguro", "javascript:alert(1)", adminOne.Id, adminOne.FullName);
    Check(!imageReject.Succeeded && imageUpload.Succeeded && videoUpload.Succeeded && !badVideo.Succeeded
        && youtube.Succeeded && !unsafeYoutube.Succeeded && !arbitraryHost.Succeeded && externalLink.Succeeded && !unsafeLink.Succeeded,
        "NOTICIAS 3-13/20", "Imágenes/MP4 válidos aceptados; formatos, dominios y protocolos no permitidos rechazados");

    var newsDetailsBeforePublish = await news.GetAdminDetailsAsync(newsDraft.Value!);
    var localVideoId = newsDetailsBeforePublish!.Media.Single(x => x.IsVideo).Id;
    var localImageId = newsDetailsBeforePublish.Media.Single(x => !x.IsVideo).Id;
    var privateMedia = await news.OpenPublicMediaAsync(localImageId);
    var privateVideo = await news.OpenPublicMediaAsync(localVideoId);
    Check(privateMedia is null && privateVideo is null, "NOTICIAS 1/8", "Medios de borrador no son accesibles por ruta pública");
    var publishedNews = await news.PublishAsync(newsDraft.Value!, adminOne.Id, adminOne.FullName);
    var publicNews = await news.GetPublicDetailsAsync(newsDraft.Value!);
    var publishedMedia = await news.OpenPublicMediaAsync(localImageId);
    var publishedVideo = await news.OpenPublicMediaAsync(localVideoId);
    Check(publishedNews.Succeeded && publicNews?.ExternalVideoUrl == "https://www.youtube-nocookie.com/embed/abcdefghijk"
        && publicNews.Media.Count == 2 && publishedMedia is not null && publishedVideo is not null,
        "NOTICIAS 2/8/9/11", "Publicación sin dependencia externa expone imagen y MP4 por endpoints controlados, y genera embed seguro");
    publishedMedia?.Content.Dispose();
    if (publishedVideo is not null)
    {
        using var videoStream = new MemoryStream();
        await publishedVideo.Value.Content.CopyToAsync(videoStream);
        Check(videoStream.ToArray().AsSpan(4, 4).SequenceEqual("ftyp"u8), "NOTICIAS 8/9", "El video local se puede leer desde storage sin Internet");
        publishedVideo.Value.Content.Dispose();
    }
    var newsPage = await news.GetPublicPageAsync("editorial", 1);
    var newsAdminMedia = await news.OpenAdminMediaAsync(newsDraft.Value!, localImageId, adminOne.Id);
    Check(newsPage.Items.Any(x => x.Id == newsDraft.Value) && newsAdminMedia is not null, "NOTICIAS 22/23", "Búsqueda pública y ruta de previsualización administrativa funcionan");
    newsAdminMedia?.Content.Dispose();

    var additionalNews = new List<int>();
    for (var i = 0; i < 10; i++)
    {
        var createdNews = await news.SaveDraftAsync(null, adminOne.Id, adminOne.FullName,
            new NewsPostInput($"Aviso editorial número {i:00}", "Resumen editorial para validar paginación pública.", "Contenido de prueba suficientemente extenso para validar el módulo editorial y sus páginas.", i == 0));
        if (!createdNews.Succeeded) throw new InvalidOperationException(createdNews.Message);
        additionalNews.Add(createdNews.Value!);
        await news.PublishAsync(createdNews.Value!, adminOne.Id, adminOne.FullName);
    }
    var firstNewsPage = await news.GetPublicPageAsync(null, 1);
    var secondNewsPage = await news.GetPublicPageAsync(null, 2);
    var highlighted = await news.GetFeaturedAsync(4);
    Check(firstNewsPage.PageSize == 9 && firstNewsPage.Items.Count == 9 && secondNewsPage.Items.Count >= 2 && highlighted.Any(x => x.IsFeatured), "NOTICIAS 25/16", "Listado público pagina a nueve y prioriza las destacadas");
    await news.SetVisibilityAsync(newsDraft.Value!, adminOne.Id, adminOne.FullName, false);
    Check(await news.GetPublicDetailsAsync(newsDraft.Value!) is null && await news.OpenPublicMediaAsync(localImageId) is null, "NOTICIAS 14/17/37", "Ocultar retira detalle y medios públicos sin eliminar metadatos");
    await news.SetVisibilityAsync(newsDraft.Value!, adminOne.Id, adminOne.FullName, true);
    var deleteNewsMedia = await news.DeleteMediaAsync(newsDraft.Value!, localVideoId, adminOne.Id, adminOne.FullName);
    dbContext.ChangeTracker.Clear();
    var deletedVideoMeta = await dbContext.MediaFiles.AsNoTracking().SingleAsync(x => x.Id == localVideoId);
    Check(deleteNewsMedia.Succeeded && deletedVideoMeta.IsDeleted
        && !File.Exists(Path.Combine(storageRoot, deletedVideoMeta.RelativePath.Replace('/', Path.DirectorySeparatorChar)))
        && await news.OpenPublicMediaAsync(localVideoId) is null, "NOTICIAS 21", "Eliminar video marca metadatos, borra el archivo físico y lo retira de la ruta pública");
    var deleteNewsImage = await news.DeleteMediaAsync(newsDraft.Value!, localImageId, adminOne.Id, adminOne.FullName);
    dbContext.ChangeTracker.Clear();
    var deletedImageMeta = await dbContext.MediaFiles.AsNoTracking().SingleAsync(x => x.Id == localImageId);
    Check(deleteNewsImage.Succeeded && deletedImageMeta.IsDeleted
        && !File.Exists(Path.Combine(storageRoot, deletedImageMeta.RelativePath.Replace('/', Path.DirectorySeparatorChar))), "NOTICIAS 7", "Eliminar imagen de noticia retira metadatos activos y archivo físico");
    var deletedPublishedPost = await news.DeleteDraftAsync(additionalNews[0], adminOne.Id, adminOne.FullName);
    Check(!deletedPublishedPost.Succeeded, "NOTICIAS 30/31", "No se permite eliminar definitivamente una noticia ya publicada");

    var adminDashboard = await mainScope.ServiceProvider.GetRequiredService<IAccountAdministrationService>().GetDashboardSummaryAsync();
    var auditDay = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
    var auditDayCount = await dbContext.AuditLogs.AsNoTracking().CountAsync(x => Microsoft.EntityFrameworkCore.EF.Functions.Like(x.CreatedAtUtc.ToString(), auditDay + "%"));
    var auditEnd = DateTimeOffset.UtcNow.AddDays(1).ToString("yyyy-MM-dd");
    var auditRangeCount = await dbContext.AuditLogs.FromSqlInterpolated($"SELECT * FROM \"AuditLogs\" WHERE substr(\"CreatedAtUtc\", 1, 10) >= {auditDay} AND substr(\"CreatedAtUtc\", 1, 10) <= {auditEnd}").CountAsync();
    var studentDashboard = await studentProjects.GetDashboardSummaryAsync(owner.Id);
    Check(adminDashboard.TotalStudents >= 4 && adminDashboard.TotalProjects >= 3 && adminDashboard.ProjectsByStatus.Count > 0
        && adminDashboard.ProjectsByCareer.Count > 0 && adminDashboard.PublishedNews >= 1,
        "DASHBOARD ADMIN", "Las métricas y desgloses administrativos se consultan desde SQLite");
    Check(studentDashboard.TotalProjects > 0 && studentDashboard.UnreadNotifications > 0,
        "DASHBOARD ALUMNO", "Los contadores personales de proyectos y avisos pendientes son reales");
    Check(auditDayCount > 0, "BITÁCORA FILTRO", "El filtro diario de SQLite se puede traducir sin cargar todas las entradas");
    Check(auditRangeCount >= auditDayCount, "BITÁCORA RANGO", "La consulta SQLite admite fechas inclusivas por rango");

    var careerService = mainScope.ServiceProvider.GetRequiredService<ICareerService>();
    var careerUpdate = await careerService.UpdateAsync(careerId, "Ingeniería en Sistemas Computacionales", "ISC", true, adminOne.Id, adminOne.FullName);
    dbContext.ChangeTracker.Clear();
    var careerAudit = await dbContext.AuditLogs.AsNoTracking().Where(x => x.Action == "Catálogo.CarreraModificada" && x.EntityId == careerId.ToString()).OrderByDescending(x => x.Id).FirstOrDefaultAsync();
    Check(careerUpdate.Succeeded && careerAudit is not null && careerAudit.UserId == adminOne.Id
        && careerAudit.UserDisplayName == adminOne.FullName && careerAudit.EntityType == "Carrera"
        && !string.IsNullOrWhiteSpace(careerAudit.Summary),
        "BITÁCORA CARRERA", "Editar una carrera persiste acción, usuario, entidad, ID y resumen");

    var catalogs = mainScope.ServiceProvider.GetRequiredService<IProjectCatalogService>();
    var gradeSave = await catalogs.SaveGradeAsync(0, "Prueba G6", 600, true, adminOne.Id, adminOne.FullName);
    var groupSave = await catalogs.SaveGroupAsync(0, "Z9", 99, true, adminOne.Id, adminOne.FullName);
    Check(gradeSave.Succeeded && groupSave.Succeeded
        && (await catalogs.GetGradesAsync()).Any(x => x.Name == "Prueba G6")
        && (await catalogs.GetGroupsAsync()).Any(x => x.Name == "Z9"),
        "CATÁLOGOS", "Grados y grupos quedan administrables y consultables sin editar SQLite");
    var storageReport = await mainScope.ServiceProvider.GetRequiredService<PortalProyectos.Application.Storage.IStorageDiagnosticsService>().ScanAsync();
    Check(storageReport.ActiveReferences >= 1 && storageReport.PhysicalFiles >= 1,
        "STORAGE DIAGNÓSTICO", "El diagnóstico compara archivos físicos y referencias sin borrarlos");

    // Development is an administrative operation, not an academic edit/version.
    dbContext.ChangeTracker.Clear();
    Check(typeof(PortalProyectos.Web.Models.Student.ProjectEditorViewModel).GetProperty("DevelopmentStatus") is null
        && typeof(PortalProyectos.Web.Areas.Student.Controllers.ProjectsController).GetMethod("StartDevelopmentStatusUpdate") is null,
        "DESARROLLO BINDING", "No hay binding ni endpoint de actualización de desarrollo del alumno");
    var superAdmin = await CreateUserAsync(mainScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
        "SUPERDEV001", "Superadmin desarrollo", "superdev@example.test", AccountStatus.Approved, SystemRoles.SuperAdministrator);
    var operationalBefore = await studentProjects.GetProjectAsync(owner.Id, projectId);
    var versionsBefore = await dbContext.ProjectVersions.CountAsync(x => x.ProjectId == projectId);
    var reviewsBefore = await dbContext.ProjectReviews.CountAsync(x => x.ProjectId == projectId);
    var notificationsBefore = await dbContext.Notifications.CountAsync();
    var publicationBefore = JsonSerializer.Serialize(await dbContext.ProjectPublications.AsNoTracking().SingleAsync(x => x.ProjectId == projectId));
    var likesBefore = await dbContext.ProjectLikes.CountAsync(x => x.ProjectId == projectId);
    var publicBefore = await publications.GetPublicDetailsAsync(projectId, owner.Id);
    Check(!(await studentProjects.UpdateDraftAsync(owner.Id, projectId, validDraft with { DevelopmentStatus = ProjectDevelopmentStatus.Completed })).Succeeded
        && !(await studentProjects.UpdateDraftAsync(linked.Id, projectId, validDraft with { DevelopmentStatus = ProjectDevelopmentStatus.Completed })).Succeeded
        && !(await studentProjects.SubmitAsync(owner.Id, owner.FullName, projectId)).Succeeded,
        "DESARROLLO ALUMNOS", "Responsable y participante no editan el aprobado ni generan otro envío");
    Check(!(await reviews.FinishAsync(projectId, owner.Id)).Succeeded
        && !(await reviews.FinishAsync(projectId, linked.Id)).Succeeded
        && !(await reviews.ReopenAsync(projectId, other.Id)).Succeeded
        && !(await reviews.FinishAsync(projectId, "missing-user")).Succeeded,
        "DESARROLLO ROLES", "Servicio rechaza responsable, participante, ajeno y usuario inexistente");
    Check(!(await reviews.FinishAsync(raceProjectId, adminOne.Id)).Succeeded
        && !(await reviews.FinishAsync(rejectedProjectId, adminOne.Id)).Succeeded
        && !(await reviews.FinishAsync(int.MaxValue, adminOne.Id)).Succeeded
        && !(await studentProjects.UpdateDraftAsync(owner.Id, raceProjectId, validDraft with { DevelopmentStatus = ProjectDevelopmentStatus.Completed })).Succeeded,
        "DESARROLLO ESTADOS", "Revisión, rechazo e inexistente no permiten transición; alumno no edita en revisión");
    var finished = await reviews.FinishAsync(projectId, adminOne.Id);
    dbContext.ChangeTracker.Clear();
    var completedProject = await studentProjects.GetProjectAsync(owner.Id, projectId);
    Check(finished.Succeeded && completedProject!.Status == ProjectAdministrativeStatus.Approved
        && completedProject.DevelopmentStatus == ProjectDevelopmentStatus.Completed,
        "DESARROLLO TERMINAR", "Administrador termina sin cambiar Aprobado");
    Check(!(await reviews.FinishAsync(projectId, adminTwo.Id)).Succeeded
        && await dbContext.AuditLogs.CountAsync(x => x.EntityId == projectId.ToString() && x.Action == "Proyecto.Terminado") == 1,
        "DESARROLLO DOBLE CLIC", "Segundo intento se rechaza sin duplicar bitácora");
    var publicCompleted = await publications.GetPublicDetailsAsync(projectId, owner.Id);
    Check(publicCompleted!.Project.DevelopmentStatus == ProjectDevelopmentStatus.Completed
        && publicCompleted.Project.VersionId == publicBefore!.Project.VersionId
        && publicCompleted.Project.Name == publicBefore.Project.Name,
        "DESARROLLO PÚBLICO", "Muestra Terminado sin reemplazar versión pública ni contenido");
    var reopened = await reviews.ReopenAsync(projectId, superAdmin.Id);
    dbContext.ChangeTracker.Clear();
    var reopenedProject = await studentProjects.GetProjectAsync(owner.Id, projectId);
    Check(reopened.Succeeded && reopenedProject!.Status == ProjectAdministrativeStatus.Approved
        && reopenedProject.DevelopmentStatus == ProjectDevelopmentStatus.InProgress
        && !(await reviews.ReopenAsync(projectId, adminOne.Id)).Succeeded,
        "DESARROLLO REABRIR", "Superadministrador reabre; doble intento no repite transición");
    var developmentRace = await Task.WhenAll(reviewOne.FinishAsync(projectId, adminOne.Id), reviewTwo.FinishAsync(projectId, superAdmin.Id));
    Check(developmentRace.Count(x => x.Succeeded) == 1,
        "DESARROLLO CONCURRENCIA", "Dos administradores compiten: una sola transición válida");
    Check((await reviews.ReopenAsync(projectId, adminOne.Id)).Succeeded,
        "DESARROLLO CICLO", "Administrador también puede reabrir después de terminar");
    Check((await reviews.FinishAsync(projectId, superAdmin.Id)).Succeeded
        && (await reviews.ReopenAsync(projectId, superAdmin.Id)).Succeeded,
        "DESARROLLO SUPERADMIN", "Superadministrador ejecuta ambas transiciones de forma explícita");
    dbContext.ChangeTracker.Clear();
    var operationalAfter = await studentProjects.GetProjectAsync(owner.Id, projectId);
    Check(versionsBefore == await dbContext.ProjectVersions.CountAsync(x => x.ProjectId == projectId)
        && reviewsBefore == await dbContext.ProjectReviews.CountAsync(x => x.ProjectId == projectId)
        && operationalAfter!.VersionId == operationalBefore!.VersionId
        && operationalAfter.Name == operationalBefore.Name && operationalAfter.ProblemStatement == operationalBefore.ProblemStatement
        && operationalAfter.ProposedSolution == operationalBefore.ProposedSolution
        && operationalAfter.SubjectId == operationalBefore.SubjectId && operationalAfter.ProjectTypeId == operationalBefore.ProjectTypeId
        && operationalAfter.SchoolCycleId == operationalBefore.SchoolCycleId && operationalAfter.Advisor == operationalBefore.Advisor
        && operationalAfter.EstimatedCompletionDate == operationalBefore.EstimatedCompletionDate,
        "DESARROLLO INTEGRIDAD ACADÉMICA", "No crea versiones/revisiones ni altera campos académicos");
    Check(notificationsBefore == await dbContext.Notifications.CountAsync(),
        "DESARROLLO SIN NOTIFICACIONES", "Terminar y Reabrir no generan avisos");
    Check(publicationBefore == JsonSerializer.Serialize(await dbContext.ProjectPublications.AsNoTracking().SingleAsync(x => x.ProjectId == projectId))
        && likesBefore == await dbContext.ProjectLikes.CountAsync(x => x.ProjectId == projectId),
        "DESARROLLO PUBLICACIÓN Y LIKES", "La publicación completa y los likes permanecen idénticos");
    var developmentAudits = await dbContext.AuditLogs.AsNoTracking().Where(x => x.EntityId == projectId.ToString()
        && (x.Action == "Proyecto.Terminado" || x.Action == "Proyecto.Reabierto")).ToListAsync();
    Check(developmentAudits.Count == 6 && developmentAudits.All(x => !string.IsNullOrWhiteSpace(x.UserId)
        && !string.IsNullOrWhiteSpace(x.UserDisplayName) && x.EntityType == "Proyecto" && !string.IsNullOrWhiteSpace(x.Summary)),
        "DESARROLLO BITÁCORA", "Ambas transiciones guardan actor, proyecto, fecha y acción en bitácora existente");
    var approvedParticipant = await studentProjects.AddLinkedParticipantAsync(owner.Id, owner.FullName, projectId, other.Id);
    var teamAfterAdd = (await studentProjects.GetProjectAsync(owner.Id, projectId))!.Participants;
    var addedParticipant = teamAfterAdd.Single(x => x.UserId == other.Id);
    Check(approvedParticipant.Succeeded
        && !(await studentProjects.AddLinkedParticipantAsync(linked.Id, linked.FullName, projectId, other.Id)).Succeeded
        && !(await studentProjects.RemoveParticipantAsync(linked.Id, linked.FullName, projectId, addedParticipant.Id)).Succeeded
        && !(await studentProjects.RemoveParticipantAsync(owner.Id, owner.FullName, projectId, teamAfterAdd.Single(x => x.IsResponsible).Id)).Succeeded
        && (await studentProjects.RemoveParticipantAsync(owner.Id, owner.FullName, projectId, addedParticipant.Id)).Succeeded,
        "EQUIPO APROBADO", "Responsable agrega/retira cuentas aprobadas; vinculado no administra y responsable no puede retirarse");
    var pendingTeamMember = await CreateUserAsync(mainScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
        "PENDINGTEAM", "Integrante pendiente", "pendingteam@example.test", AccountStatus.Pending, SystemRoles.Student, careerId, gradeId, groupId);
    Check(!(await studentProjects.AddLinkedParticipantAsync(owner.Id, owner.FullName, projectId, pendingTeamMember.Id)).Succeeded,
        "EQUIPO APROBADO VALIDACIÓN", "Cuenta pendiente sigue bloqueada después de aprobación del proyecto");
    Check((await documents.GetForProjectOwnerAsync(owner.Id, pdfProject.Value)) is not null
        && (await documents.GetForLinkedParticipantAsync(linked.Id, pdfProject.Value)) is not null,
        "PDF SIN REGRESIÓN", "Documento aprobado continúa accesible al responsable y participante");
    Check(typeof(PortalProyectos.Web.Areas.Admin.Controllers.ProjectsController).GetMethod("Finish")!.GetCustomAttribute<HttpPostAttribute>() is not null
        && typeof(PortalProyectos.Web.Areas.Admin.Controllers.ProjectsController).GetMethod("Reopen")!.GetCustomAttribute<HttpPostAttribute>() is not null
        && typeof(PortalProyectos.Web.Areas.Admin.Controllers.ProjectsController).GetCustomAttribute<AuthorizeAttribute>()?.Policy == "Administracion",
        "DESARROLLO ENDPOINTS", "Acciones solo POST con política administrativa");

    await AccountLifecycleTests.RunAsync(provider, adminOne.Id, superAdmin.Id, careerId, gradeId, groupId);
    Console.WriteLine("RESULTADO: Regresión funcional Fases 1-5 y consolidación Fase 6 superadas.");
    if (retainUiFixture) Console.WriteLine("UI_FIXTURE=" + testRoot);
    return 0;
}

catch (Exception exception)
{
    Console.Error.WriteLine($"FALLO: {exception}");
    return 1;
}

finally
{
    if (provider is not null) await provider.DisposeAsync();
    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    if (!retainUiFixture && Directory.Exists(testRoot) && Path.GetFullPath(testRoot).StartsWith(Path.Combine(Path.GetTempPath(), "PortalProyectosFunctionalTests"), StringComparison.OrdinalIgnoreCase))
        Directory.Delete(testRoot, recursive: true);
}

static async Task<ApplicationUser> setupScopeUser(ServiceProvider provider, string enrollment)
{
    await using var scope = provider.CreateAsyncScope();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    return await users.Users.SingleAsync(x => x.EnrollmentNumber == enrollment);
}

static PortalProyectos.Web.Controllers.NotificationsController CreateNotificationController(INotificationService service, string userId)
{
    var context = new DefaultHttpContext();
    context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "FunctionalTest"));
    var actionContext = new ControllerContext
    {
        HttpContext = context,
        RouteData = new RouteData(),
        ActionDescriptor = new ControllerActionDescriptor()
    };
    return new PortalProyectos.Web.Controllers.NotificationsController(service)
    {
        ControllerContext = actionContext,
        Url = new UrlHelper(actionContext)
    };
}

static async Task<ApplicationUser> CreateUserAsync(
    UserManager<ApplicationUser> manager,
    string enrollment,
    string fullName,
    string email,
    AccountStatus status,
    string role,
    int? careerId = null,
    int? gradeId = null,
    int? groupId = null)
{
    var user = new ApplicationUser
    {
        FullName = fullName,
        EnrollmentNumber = enrollment,
        NormalizedEnrollmentNumber = enrollment.ToUpperInvariant(),
        UserName = enrollment,
        Email = email,
        EmailConfirmed = true,
        AccountStatus = status,
        CareerId = careerId,
        GradeId = gradeId,
        AcademicGroupId = groupId,
        ApprovedAtUtc = status == AccountStatus.Approved ? DateTimeOffset.UtcNow : null
    };
    var created = await manager.CreateAsync(user, "Functional!2026");
    if (!created.Succeeded) throw new InvalidOperationException(string.Join("; ", created.Errors.Select(x => x.Description)));
    var assigned = await manager.AddToRoleAsync(user, role);
    if (!assigned.Succeeded) throw new InvalidOperationException(string.Join("; ", assigned.Errors.Select(x => x.Description)));
    return user;
}

static ProjectDraftData ValidDraft(string name, int subjectId, int projectTypeId, int cycleId) => new(
    name,
    subjectId,
    projectTypeId,
    cycleId,
    ProjectDevelopmentStatus.InProgress,
    LongText("El problema consiste en la falta de un mecanismo ordenado y verificable para dar seguimiento al trabajo académico de los equipos"),
    LongText("La propuesta establece una solución modular que registra el avance, aplica validaciones y conserva un historial confiable para revisión"),
    "Asesor de prueba",
    DateOnly.FromDateTime(DateTime.Today.AddMonths(2)));

static string LongText(string seed) => $"{seed}. {seed}.";

static async Task<int> CreateValidSubmittedProjectAsync(
    IStudentProjectService projects,
    ApplicationUser owner,
    int subjectId,
    int typeId,
    int cycleId,
    string name)
{
    var created = await projects.CreateDraftAsync(owner.Id, owner.FullName);
    if (!created.Succeeded) throw new InvalidOperationException(created.Message);
    var id = created.Value;
    var saved = await projects.UpdateDraftAsync(owner.Id, id, ValidDraft(name, subjectId, typeId, cycleId));
    if (!saved.Succeeded) throw new InvalidOperationException(saved.Message);
    var submitted = await projects.SubmitAsync(owner.Id, owner.FullName, id);
    if (!submitted.Succeeded) throw new InvalidOperationException(submitted.Message);
    return id;
}

static void Check(bool condition, string number, string description)
{
    if (!condition) throw new InvalidOperationException($"{number} no cumplió: {description}");
    Console.WriteLine($"PASS {number}: {description}");
}

static async Task<bool> VerifyLiveSignalRAsync()
{
    var builder = WebApplication.CreateBuilder();
    builder.WebHost.UseUrls("http://127.0.0.1:0");
    builder.Logging.ClearProviders();
    builder.Services.AddAuthentication("SignalRTest")
        .AddScheme<AuthenticationSchemeOptions, SignalRTestAuthenticationHandler>("SignalRTest", _ => { });
    builder.Services.AddAuthorization();
    builder.Services.AddSignalR();
    await using var app = builder.Build();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapHub<NotificationHub>("/hubs/notifications");
    await app.StartAsync();
    try
    {
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var userSocket = await ConnectHubAsync(address, "test-user=live-recipient");
        var otherSocket = await ConnectHubAsync(address, "test-user=other-recipient");
        try
        {
            var item = new NotificationItem(88001, NotificationType.ProjectApproved, "Proyecto aprobado", "Aviso de prueba", "Project", "21", "/Student/Projects/Details/21", false, DateTimeOffset.UtcNow);
            var publisher = new PortalProyectos.Web.Hubs.SignalRNotificationPublisher(app.Services.GetRequiredService<IHubContext<NotificationHub>>());
            await publisher.PublishAsync("live-recipient", item);
            var received = await ReceiveSignalRMessageAsync(userSocket, TimeSpan.FromSeconds(3));
            using var json = JsonDocument.Parse(received);
            var targeted = json.RootElement.GetProperty("target").GetString() == "ReceiveNotification"
                && json.RootElement.GetProperty("arguments")[0].GetProperty("id").GetInt64() == item.Id;
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(350));
            try
            {
                await ReceiveSignalRMessageWithTokenAsync(otherSocket, timeout.Token);
                return false;
            }
            catch (OperationCanceledException) { return targeted; }
        }
        finally
        {
            userSocket.Dispose();
            otherSocket.Dispose();
        }
    }
    finally
    {
        await app.StopAsync();
    }
}

static async Task<ClientWebSocket> ConnectHubAsync(string address, string cookie)
{
    using var client = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false });
    using var negotiateRequest = new HttpRequestMessage(HttpMethod.Post, address.TrimEnd('/') + "/hubs/notifications/negotiate?negotiateVersion=1");
    negotiateRequest.Headers.Add("Cookie", cookie);
    using var negotiateResponse = await client.SendAsync(negotiateRequest);
    negotiateResponse.EnsureSuccessStatusCode();
    using var negotiation = JsonDocument.Parse(await negotiateResponse.Content.ReadAsStringAsync());
    var token = negotiation.RootElement.GetProperty("connectionToken").GetString()!;
    var endpoint = new UriBuilder(new Uri(address)) { Scheme = "ws", Path = "/hubs/notifications", Query = "id=" + Uri.EscapeDataString(token) };
    var socket = new ClientWebSocket();
    socket.Options.SetRequestHeader("Cookie", cookie);
    await socket.ConnectAsync(endpoint.Uri, CancellationToken.None);
    var handshake = Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e");
    await socket.SendAsync(handshake, WebSocketMessageType.Text, true, CancellationToken.None);
    var response = await ReceiveSignalRMessageAsync(socket, TimeSpan.FromSeconds(3));
    if (response != "{}") throw new InvalidOperationException("SignalR no completó el handshake JSON.");
    return socket;
}

static async Task<string> ReceiveSignalRMessageAsync(ClientWebSocket socket, TimeSpan timeout) =>
    await ReceiveSignalRMessageWithTokenAsync(socket, new CancellationTokenSource(timeout).Token);

static async Task<string> ReceiveSignalRMessageWithTokenAsync(ClientWebSocket socket, CancellationToken cancellationToken)
{
    var buffer = new byte[4096];
    using var message = new MemoryStream();
    while (true)
    {
        var result = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken);
        if (result.MessageType == WebSocketMessageType.Close) throw new WebSocketException("SignalR cerró inesperadamente la conexión.");
        message.Write(buffer, 0, result.Count);
        var text = Encoding.UTF8.GetString(message.ToArray());
        var separator = text.IndexOf('\u001e');
        if (separator >= 0) return text[..separator];
    }
}

sealed class FunctionalTestHostEnvironment : IHostEnvironment
{
    public FunctionalTestHostEnvironment(string contentRootPath) : this()
    {
        Directory.CreateDirectory(contentRootPath);
        ContentRootPath = contentRootPath;
        ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
    }

    private FunctionalTestHostEnvironment() { }
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "PortalProyectos.FunctionalTests";
    public string ContentRootPath { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

sealed class RecordingNotificationPublisher : INotificationPublisher
{
    public List<(string UserId, NotificationItem Notification)> Deliveries { get; } = [];
    public List<(string UserId, int UnreadCount)> UnreadUpdates { get; } = [];
    public bool Fail { get; set; }
    public Task PublishAsync(string userId, NotificationItem notification, CancellationToken cancellationToken = default)
    {
        if (Fail) throw new IOException("SignalR no disponible durante la prueba.");
        Deliveries.Add((userId, notification));
        return Task.CompletedTask;
    }
    public Task PublishUnreadCountAsync(string userId, int unreadCount, CancellationToken cancellationToken = default)
    {
        if (Fail) throw new IOException("SignalR no disponible durante la prueba.");
        UnreadUpdates.Add((userId, unreadCount));
        return Task.CompletedTask;
    }
}

sealed class FunctionalTestFileStorageService(string root) : IFileStorageService
{
    private readonly string _root = Path.GetFullPath(root);
    public bool FailNextSave { get; set; }

    public async Task<StoredFileDescriptor> SaveAsync(Stream content, string relativeDirectory, string extension, CancellationToken cancellationToken = default)
    {
        if (FailNextSave)
        {
            FailNextSave = false;
            throw new IOException("Fallo de storage inducido por prueba funcional.");
        }
        var segments = relativeDirectory.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(x => x is "." or "..")) throw new InvalidOperationException("Ruta de prueba inválida.");
        var directory = Path.Combine(new[] { _root }.Concat(segments).ToArray());
        Directory.CreateDirectory(directory);
        var name = $"{Guid.NewGuid():N}{extension}";
        var destination = Path.Combine(directory, name);
        await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            await content.CopyToAsync(output, cancellationToken);
        return new StoredFileDescriptor(name, string.Join('/', segments.Append(name)), new FileInfo(destination).Length);
    }

    public Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var path = Resolve(relativePath);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var path = Resolve(relativePath);
        if (!File.Exists(path)) return Task.FromResult(false);
        File.Delete(path);
        return Task.FromResult(true);
    }

    private string Resolve(string relativePath)
    {
        if (Path.IsPathRooted(relativePath)) throw new InvalidOperationException("Ruta de prueba absoluta no permitida.");
        var path = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ruta de prueba fuera del root.");
        return path;
    }
}

sealed class SignalRTestAuthenticationHandler(
    Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
    Microsoft.Extensions.Logging.ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var cookie = Request.Headers.Cookie.FirstOrDefault()?.Split(';').Select(x => x.Trim())
            .FirstOrDefault(x => x.StartsWith("test-user=", StringComparison.Ordinal));
        if (cookie is null) return Task.FromResult(AuthenticateResult.NoResult());
        var userId = cookie["test-user=".Length..];
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Name, userId)], Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

sealed class FunctionalTestTempDataProvider : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}
