# Portal de Proyectos

Aplicación web para registrar y revisar proyectos académicos, administrar usuarios y catálogos, publicar proyectos y noticias, y consultar documentos privados. Incluye los roles Alumno, Administrador y SuperAdministrador.

Está construida con ASP.NET Core 10 MVC, Identity, Entity Framework Core y SQLite. Los recursos y archivos locales funcionan sin Internet; los videos externos de YouTube requieren conexión.

## Manual y usuarios

- [Manual de uso editable](docs/Manual_Uso_e_Instalacion_Completo.pptx).
- [Manual de uso en PDF](docs/Manual_Uso_e_Instalacion_Completo.pdf).
- Las credenciales de demostración son datos locales y no deben publicarse ni reutilizarse en instalaciones reales. Genera cuentas de prueba únicas al preparar cada entrega.

El manual es la documentación de uso vigente. Las imágenes del manual están incrustadas en el PowerPoint. Las carpetas locales `imagenes-manual/` y `credenciales-manual/` están excluidas de Git.

> **Aviso de seguridad:** una versión anterior del repositorio contenía un documento de credenciales de demostración. Aunque se retire de la rama actual, puede persistir en el historial Git. Si alguna cuenta o contraseña fue reutilizada en una instalación accesible, revoca sus sesiones y cámbiala antes de distribuir o desplegar el proyecto.

## Ejecutar el paquete con los datos actuales

En la copia local, el paquete para Windows x64 está en `artifacts/release/PortalProyectos-PrimeraEntrega-win-x64`. Incluye el entorno de ejecución de .NET y los datos creados durante la elaboración del manual. No necesita el SDK para arrancar. El paquete, sus datos y las credenciales no se publican en este repositorio.

Desde la raíz del proyecto, en PowerShell:

```powershell
Set-Location artifacts/release/PortalProyectos-PrimeraEntrega-win-x64/app
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ASPNETCORE_URLS = "http://localhost:5000"
.\PortalProyectos.Web.exe
```

Abre `http://localhost:5000`. Para atender a otros equipos de la red, configura la dirección y el puerto apropiados. También puedes establecer `Server:Urls` en `app/appsettings.Production.json`; si tiene un valor, prevalece sobre `ASPNETCORE_URLS`.

No necesitas crear nuevamente el SuperAdministrador ni configurar las variables `Portal__SeedAdmin__*` para usar esta entrega con sus datos existentes.

## Archivos que deben conservarse

Dentro del paquete:

- `app`: ejecutable, dependencias y configuración.
- `data/portalproyectos.db`: usuarios, catálogos, proyectos, publicaciones e historial.
- `data/keys`: claves de protección de datos.
- `storage`: PDF, imágenes y videos subidos, organizados por proyecto o noticia.

Para mover la aplicación, copia el paquete completo. Para respaldarla, detén primero la aplicación y conserva juntos `data`, `storage` y la configuración de `app`. Incluye los archivos asociados a SQLite si existen. El proyecto mantiene `scripts/backup.ps1` como herramienta de respaldo.

Los PDF reemplazados en una misma versión se eliminan físicamente después de guardar correctamente el reemplazo; se conserva su registro histórico. Los documentos de otras versiones no se eliminan por esa operación.

## Código fuente

- `src/PortalProyectos.Domain`: entidades, estados y roles.
- `src/PortalProyectos.Application`: contratos y casos de uso.
- `src/PortalProyectos.Infrastructure`: persistencia, almacenamiento y servicios.
- `src/PortalProyectos.Web`: controladores, vistas y recursos web.
- `tests/PortalProyectos.FunctionalTests`: pruebas funcionales con datos temporales independientes.

Para compilar el código se necesita el SDK de .NET 10 y restaurar los paquetes NuGet. Las carpetas `bin` y `obj` son regenerables; eliminarlas no afecta los datos del paquete publicado.

Para arrancar una instalación nueva desde el código, en PowerShell desde la raíz:

```powershell
dotnet restore PortalProyectos.sln
$env:Portal__SeedAdmin__Email = Read-Host "Correo del SuperAdministrador inicial"
$seedPassword = Read-Host "Contraseña temporal" -AsSecureString
$env:Portal__SeedAdmin__Password = [System.Net.NetworkCredential]::new("", $seedPassword).Password
dotnet run --project src/PortalProyectos.Web --launch-profile http
```

Abre `http://localhost:5116`. La contraseña inicial debe cumplir los requisitos de Identity y se cambia durante el primer acceso. Después de detener el proceso, retira las variables de creación de la sesión; no hacen falta para los arranques siguientes:

```powershell
Remove-Item Env:Portal__SeedAdmin__Email, Env:Portal__SeedAdmin__Password -ErrorAction SilentlyContinue
Remove-Variable seedPassword -ErrorAction SilentlyContinue
```

Ejecutar desde el código fuente con la configuración predeterminada utiliza rutas de desarrollo distintas a las del paquete. Para consultar los datos actuales, arranca el paquete indicado arriba.

Los archivos de licencia de las bibliotecas se conservan junto a sus recursos.
