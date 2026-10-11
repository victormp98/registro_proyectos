(() => {
  "use strict";

  const body = document.body;
  // Preserve table information when responsive layouts switch rows into labeled cards.
  document.querySelectorAll(".table-responsive table").forEach((table) => {
    const headers = [...table.querySelectorAll("thead th")].map((header) => header.textContent.trim());
    table.querySelectorAll("tbody tr").forEach((row) => {
      [...row.cells].forEach((cell, index) => {
        if (!cell.hasAttribute("colspan") && headers[index]) cell.dataset.label = headers[index];
      });
    });
  });
  document.querySelectorAll("[data-sidebar-open]").forEach((button) =>
    button.addEventListener("click", () => body.classList.add("sidebar-open")));
  document.querySelectorAll("[data-sidebar-close]").forEach((button) =>
    button.addEventListener("click", () => body.classList.remove("sidebar-open")));

  document.querySelectorAll(".portal-toast").forEach((element) => {
    if (window.bootstrap) {
      window.bootstrap.Toast.getOrCreateInstance(element).show();
    }
  });

  document.querySelectorAll("[data-password-toggle]").forEach((button) => {
    button.addEventListener("click", () => {
      const input = document.getElementById(button.getAttribute("aria-controls"));
      if (!(input instanceof HTMLInputElement)) return;
      const showing = input.type === "password";
      input.type = showing ? "text" : "password";
      const label = showing ? "Ocultar contraseña" : "Mostrar contraseña";
      button.setAttribute("aria-label", label);
      button.setAttribute("aria-pressed", String(showing));
      button.title = label;
      button.querySelector("use")?.setAttribute("href", showing ? "/assets/icons.svg#icon-eye-off" : "/assets/icons.svg#icon-eye");
    });
  });

  const revealItems = document.querySelectorAll(".reveal");
  const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  if ("IntersectionObserver" in window && !reducedMotion) {
    const observer = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          entry.target.classList.add("is-visible");
          observer.unobserve(entry.target);
        }
      });
    }, { threshold: 0.12 });
    revealItems.forEach((item) => observer.observe(item));
  } else {
    revealItems.forEach((item) => item.classList.add("is-visible"));
  }

  document.querySelectorAll("[data-counter]").forEach((element) => {
    const target = Number.parseInt(element.dataset.counter ?? "0", 10);
    if (!Number.isFinite(target) || target <= 0 || reducedMotion) {
      element.textContent = Number.isFinite(target) ? target.toString() : "0";
      return;
    }

    const start = performance.now();
    const duration = 650;
    const animate = (now) => {
      const progress = Math.min((now - start) / duration, 1);
      element.textContent = Math.round(target * (1 - Math.pow(1 - progress, 3))).toString();
      if (progress < 1) requestAnimationFrame(animate);
    };
    requestAnimationFrame(animate);
  });

  document.querySelectorAll("[data-reason-modal]").forEach((button) => {
    button.addEventListener("click", () => {
      const form = document.querySelector(button.dataset.formTarget);
      const title = document.querySelector("#reasonModalTitle");
      const description = document.querySelector("#reasonModalDescription");
      if (!form) return;
      form.action = button.dataset.actionUrl;
      form.querySelector("input[name='id']").value = button.dataset.userId;
      if (title) title.textContent = button.dataset.title;
      if (description) description.textContent = button.dataset.description;
    });
  });

  document.querySelectorAll("form[data-confirm]").forEach((form) => {
    form.addEventListener("submit", (event) => {
      if (!window.confirm(form.dataset.confirm)) event.preventDefault();
    });
  });

  document.querySelectorAll("[data-submit-once]").forEach((button) => {
    button.form?.addEventListener("submit", () => {
      window.setTimeout(() => {
        button.disabled = true;
        button.setAttribute("aria-busy", "true");
      }, 0);
    });
  });

  document.querySelectorAll("textarea[maxlength], input[maxlength]").forEach((field) => {
    const counter = field.parentElement?.querySelector("[data-char-count]");
    if (!counter) return;
    const updateCounter = () => {
      counter.textContent = `${field.value.length} / ${field.maxLength}`;
    };
    field.addEventListener("input", updateCounter);
    updateCounter();
  });

  document.querySelectorAll("[data-project-form]").forEach((form) => {
    form.addEventListener("submit", (event) => {
      if (!event.submitter?.matches("[data-submit-project]")) return;
      let valid = true;
      form.querySelectorAll("[data-submit-required]").forEach((field) => {
        const value = field.value.trim();
        const minimum = Number.parseInt(field.dataset.minlength ?? "1", 10);
        const fieldValid = value.length >= minimum;
        field.classList.toggle("is-invalid", !fieldValid);
        valid = valid && fieldValid;
      });
      if (!valid) {
        event.preventDefault();
        form.querySelector(".is-invalid")?.focus();
        return;
      }
      if (!window.confirm("¿Enviar el proyecto a revisión? No podrás editarlo mientras administración lo revisa.")) {
        event.preventDefault();
        return;
      }
      window.setTimeout(() => {
        form.querySelectorAll("button[type='submit']").forEach((button) => button.disabled = true);
      }, 0);
    });
  });

  document.querySelectorAll("[data-participant-search]").forEach((button) => {
    button.addEventListener("click", async () => {
      const input = document.querySelector("#linkedStudentNumber");
      const result = document.querySelector("[data-linked-result]");
      const confirmForm = document.querySelector("[data-linked-confirm-form]");
      if (!input || !result || !confirmForm) return;
      result.innerHTML = '<span class="text-secondary">Buscando…</span>';
      confirmForm.classList.add("d-none");
      const url = `${button.dataset.url}?projectId=${encodeURIComponent(button.dataset.projectId)}&studentNumber=${encodeURIComponent(input.value)}`;
      try {
        const response = await fetch(url, { headers: { "X-Requested-With": "XMLHttpRequest" } });
        const payload = await response.json();
        if (!response.ok || !payload.ok) throw new Error(payload.message ?? "No se encontró al alumno.");
        const student = payload.student;
        result.replaceChildren();
        const card = document.createElement("article");
        card.className = "candidate-card";
        const avatar = document.createElement("span");
        avatar.className = "avatar";
        avatar.textContent = student.fullName.charAt(0);
        const details = document.createElement("div");
        const name = document.createElement("strong");
        name.textContent = student.fullName;
        const academic = document.createElement("span");
        academic.textContent = `${student.studentNumber} · ${student.career}`;
        const group = document.createElement("small");
        group.textContent = `${student.grade} · Grupo ${student.group}`;
        details.append(name, academic, group);
        card.append(avatar, details);
        result.append(card);
        confirmForm.querySelector("input[name='linkedUserId']").value = student.userId;
        confirmForm.classList.remove("d-none");
      } catch (error) {
        result.replaceChildren();
        const message = document.createElement("div");
        message.className = "validation-summary-errors";
        message.textContent = error.message;
        result.append(message);
      }
    });
  });

  document.querySelectorAll("[data-changes-form]").forEach((form) => {
    form.addEventListener("submit", (event) => {
      const hasNotes = form.querySelector("textarea[name='Notes']")?.value.trim().length > 0;
      const hasRequirement = form.querySelector("input[name='MissingRequirementIds']:checked") !== null;
      const error = form.querySelector("[data-changes-error]");
      if (!hasNotes && !hasRequirement) {
        event.preventDefault();
        error?.classList.remove("d-none");
      } else {
        error?.classList.add("d-none");
      }
    });
  });

  const galleryTriggers = Array.from(document.querySelectorAll("[data-project-gallery-image]"));
  const lightbox = document.querySelector("[data-project-lightbox]");
  if (galleryTriggers.length && lightbox && window.bootstrap?.Modal) {
    const lightboxInstance = window.bootstrap.Modal.getOrCreateInstance(lightbox, { backdrop: true, keyboard: true, focus: true });
    const lightboxImage = lightbox.querySelector("[data-lightbox-image]");
    const lightboxPosition = lightbox.querySelector("[data-lightbox-position]");
    const previousButton = lightbox.querySelector("[data-lightbox-previous]");
    const nextButton = lightbox.querySelector("[data-lightbox-next]");
    let activeImage = 0;
    const displayImage = (index) => {
      activeImage = (index + galleryTriggers.length) % galleryTriggers.length;
      const trigger = galleryTriggers[activeImage];
      if (lightboxImage) {
        lightboxImage.src = trigger.dataset.imageUrl ?? "";
        lightboxImage.alt = trigger.dataset.imageAlt ?? "Imagen del proyecto";
      }
      if (lightboxPosition) lightboxPosition.textContent = `${activeImage + 1} de ${galleryTriggers.length}`;
      if (previousButton) previousButton.disabled = galleryTriggers.length < 2;
      if (nextButton) nextButton.disabled = galleryTriggers.length < 2;
    };

    galleryTriggers.forEach((trigger, index) => trigger.addEventListener("click", () => {
      displayImage(index);
      lightboxInstance.show(trigger);
    }));
    previousButton?.addEventListener("click", () => displayImage(activeImage - 1));
    nextButton?.addEventListener("click", () => displayImage(activeImage + 1));
    lightbox.addEventListener("keydown", (event) => {
      if (event.key === "ArrowLeft" && galleryTriggers.length > 1) {
        event.preventDefault();
        displayImage(activeImage - 1);
      } else if (event.key === "ArrowRight" && galleryTriggers.length > 1) {
        event.preventDefault();
        displayImage(activeImage + 1);
      }
    });
    lightbox.addEventListener("hidden.bs.modal", () => {
      if (lightboxImage) lightboxImage.removeAttribute("src");
    });
  }

  const notificationWidget = document.querySelector("[data-notification-widget]");
  if (notificationWidget) {
    const badge = notificationWidget.querySelector("[data-notification-count]");
    const summary = notificationWidget.querySelector("[data-notification-summary]");
    const list = notificationWidget.querySelector("[data-notification-list]");
    const dropdown = notificationWidget.querySelector("[data-notification-dropdown]");
    const toggle = notificationWidget.querySelector("[data-notification-toggle]");
    const token = notificationWidget.querySelector("input[name='__RequestVerificationToken']")?.value;
    let unreadCount = Number.parseInt(badge?.textContent ?? "0", 10) || 0;

    const positionDropdown = () => {
      if (!dropdown || dropdown.hidden || !toggle) return;
      const trigger = toggle.getBoundingClientRect();
      const widget = notificationWidget.getBoundingClientRect();
      const viewportWidth = document.documentElement.clientWidth;
      const width = dropdown.getBoundingClientRect().width;
      const maxLeft = Math.max(12, viewportWidth - width - 12);
      const left = Math.min(Math.max(12, trigger.right - width), maxLeft);
      dropdown.style.left = `${left - widget.left}px`;
      const viewportHeight = document.documentElement.clientHeight;
      const gap = 8;
      const spaceBelow = Math.max(0, viewportHeight - trigger.bottom - gap - 12);
      const spaceAbove = Math.max(0, trigger.top - gap - 12);
      const openAbove = spaceBelow < Math.min(260, dropdown.scrollHeight) && spaceAbove > spaceBelow;
      const availableHeight = openAbove ? spaceAbove : spaceBelow;
      dropdown.style.top = openAbove ? "auto" : `${trigger.bottom + gap - widget.top}px`;
      dropdown.style.bottom = openAbove ? `${widget.bottom - trigger.top + gap}px` : "auto";
      dropdown.style.maxHeight = `${availableHeight}px`;
    };

    const setUnread = (count) => {
      unreadCount = Math.max(0, count);
      if (badge) {
        badge.textContent = unreadCount > 99 ? "99+" : unreadCount.toString();
        badge.classList.toggle("d-none", unreadCount === 0);
      }
      if (summary) summary.textContent = `${unreadCount} sin leer`;
      toggle?.setAttribute("aria-label", `Notificaciones, ${unreadCount} sin leer`);
    };

    const iconFor = (type) => {
      if (["AccountApproved", "ProjectApproved"].includes(type)) return "icon-check";
      if (["AccountRejected", "AccountBlocked", "ProjectRejected"].includes(type)) return "icon-shield";
      if (type.startsWith("Project")) return "icon-folder";
      if (type === "AccountPending") return "icon-user";
      return "icon-bell";
    };

    const buildNotification = (item) => {
      const article = document.createElement("article");
      article.className = `notification-popover-item${item.isRead ? " is-read" : " is-unread"}`;
      article.dataset.notificationId = item.id;
      const icon = document.createElement("span");
      icon.className = "notification-icon";
      icon.innerHTML = `<svg class="icon" aria-hidden="true"><use href="/assets/icons.svg#${iconFor(item.type)}"></use></svg>`;
      const content = document.createElement("span");
      content.className = "notification-content";
      const title = document.createElement("strong");
      title.textContent = item.title;
      const message = document.createElement("span");
      message.textContent = item.message;
      const time = document.createElement("time");
      time.dateTime = item.createdAtUtc;
      time.textContent = new Intl.DateTimeFormat("es-MX", { dateStyle: "short", timeStyle: "short" }).format(new Date(item.createdAtUtc));
      content.append(title, message, time);
      if (!item.isRead) {
        const dot = document.createElement("span");
        dot.className = "notification-unread-dot";
        dot.setAttribute("aria-label", "No leída");
        article.append(icon, content, dot);
      } else article.append(icon, content);
      const link = document.createElement("a");
      link.href = `/Notifications/${encodeURIComponent(item.id)}/open`;
      link.className = "notification-item-link";
      link.textContent = "Abrir";
      link.className = "btn btn-sm btn-outline-primary notification-open-link";
      link.setAttribute("aria-label", `Abrir y marcar como leída: ${item.title}`);
      const actions = document.createElement("div");
      actions.className = "notification-item-actions";
      actions.append(link);
      if (!item.isRead) {
        const markRead = document.createElement("button");
        markRead.type = "button";
        markRead.className = "btn btn-sm btn-ghost";
        markRead.dataset.notificationMarkRead = "";
        markRead.textContent = "Marcar como leída";
        actions.append(markRead);
      }
      article.append(actions);
      return article;
    };

    const showItems = (items) => {
      if (!list) return;
      if (!Array.isArray(items)) throw new Error("La respuesta reciente no es una lista de notificaciones.");
      list.replaceChildren();
      if (!items.length) {
        const empty = document.createElement("div");
        empty.className = "notification-loading";
        empty.textContent = "No tienes avisos pendientes.";
        list.append(empty);
        requestAnimationFrame(positionDropdown);
        return;
      }
      items.forEach((item) => list.append(buildNotification(item)));
      requestAnimationFrame(positionDropdown);
    };

    const loadRecent = async () => {
      if (!list) return;
      try {
        const response = await fetch(notificationWidget.dataset.recentUrl, { credentials: "same-origin", headers: { "X-Requested-With": "XMLHttpRequest", "Accept": "application/json" } });
        if (!response.ok) throw new Error("No fue posible cargar las notificaciones.");
        const contentType = response.headers.get("content-type") ?? "";
        if (!contentType.includes("application/json")) throw new Error("El servidor no devolvió JSON.");
        const payload = await response.json();
        if (!payload || !Array.isArray(payload.items) || !Number.isFinite(payload.unreadCount)) throw new Error("La respuesta de avisos no tiene el formato esperado.");
        setUnread(payload.unreadCount);
        showItems(payload.items);
      } catch {
        list.textContent = "No fue posible cargar los avisos. Intenta de nuevo.";
      }
    };

    list?.addEventListener("click", async (event) => {
      const button = event.target.closest("[data-notification-mark-read]");
      if (!button || !token) return;
      event.preventDefault();
      const article = button.closest("[data-notification-id]");
      if (!article) return;
      button.disabled = true;
      try {
        const response = await fetch(`/Notifications/${encodeURIComponent(article.dataset.notificationId)}/read`, {
          method: "POST", credentials: "same-origin",
          headers: { "RequestVerificationToken": token, "X-Requested-With": "XMLHttpRequest" }
        });
        if (!response.ok) throw new Error("No fue posible marcar el aviso.");
        const result = await response.json();
        setUnread(result.unreadCount);
        article.remove();
        if (!list.querySelector("[data-notification-id]")) {
          const empty = document.createElement("div");
          empty.className = "notification-loading";
          empty.textContent = "No tienes avisos pendientes.";
          list.append(empty);
        }
        requestAnimationFrame(positionDropdown);
      } catch {
        button.disabled = false;
      }
    });

    toggle?.addEventListener("click", async () => {
      const opening = dropdown?.hidden ?? true;
      if (dropdown) dropdown.hidden = !opening;
      toggle.setAttribute("aria-expanded", opening.toString());
      if (opening) {
        positionDropdown();
        await loadRecent();
      }
    });

    window.addEventListener("resize", positionDropdown);
    document.addEventListener("scroll", positionDropdown, true);

    document.addEventListener("click", (event) => {
      if (!notificationWidget.contains(event.target) && dropdown && !dropdown.hidden) {
        dropdown.hidden = true;
        toggle?.setAttribute("aria-expanded", "false");
      }
    });

    document.querySelectorAll("[data-mark-all-notifications]").forEach((form) => {
      form.addEventListener("submit", async (event) => {
        event.preventDefault();
        const response = await fetch(form.action, { method: "POST", body: new FormData(form), credentials: "same-origin", headers: { "RequestVerificationToken": token ?? "", "X-Requested-With": "XMLHttpRequest" } });
        if (response.ok) location.reload();
      });
    });

    document.querySelectorAll("[data-document-input]").forEach((input) => {
      input.addEventListener("change", () => {
        const file = input.files?.[0];
        const label = document.querySelector("[data-document-filename]");
        if (!file) return;
        if (!file.name.toLowerCase().endsWith(".pdf") || file.type !== "application/pdf" || file.size > 10 * 1024 * 1024) {
          input.value = "";
          if (label) label.textContent = "Elige un PDF válido de hasta 10 MB.";
          return;
        }
        if (label) label.textContent = `${file.name} · ${(file.size / 1024 / 1024).toFixed(1)} MB`;
      });
    });

    document.querySelectorAll("[data-document-form]").forEach((form) => form.addEventListener("submit", (event) => {
      const input = form.querySelector("[data-document-input]");
      const file = input?.files?.[0];
      if (!file || !file.name.toLowerCase().endsWith(".pdf") || file.type !== "application/pdf" || file.size > 10 * 1024 * 1024) {
        event.preventDefault();
        input?.focus();
        return;
      }
      if (form.dataset.documentReplace === "True" && !window.confirm("El PDF actual dejará de estar vigente al completar el reemplazo. ¿Continuar?")) event.preventDefault();
    }));

    const showLiveToast = (item) => {
      let container = document.querySelector("[data-live-toasts]");
      if (!container) {
        container = document.createElement("div");
        container.className = "live-toast-container";
        container.dataset.liveToasts = "";
        container.setAttribute("aria-live", "polite");
        container.setAttribute("aria-atomic", "false");
        document.body.append(container);
      }
      const toast = document.createElement("article");
      toast.className = "live-notification-toast";
      toast.setAttribute("role", "status");
      const heading = document.createElement("strong");
      heading.textContent = item.title;
      const message = document.createElement("span");
      message.textContent = item.message;
      const close = document.createElement("button");
      close.type = "button";
      close.textContent = "×";
      close.setAttribute("aria-label", "Cerrar aviso");
      close.addEventListener("click", () => toast.remove());
      toast.append(heading, message, close);
      container.append(toast);
      window.setTimeout(() => toast.remove(), 6500);
    };

    if (window.signalR) {
      const connection = new window.signalR.HubConnectionBuilder()
        .withUrl(notificationWidget.dataset.hubUrl)
        .withAutomaticReconnect()
        .configureLogging(window.signalR.LogLevel.Warning)
        .build();
      connection.on("ReceiveNotification", (item) => {
        if (document.querySelector(`[data-notification-id="${CSS.escape(item.id.toString())}"]`)) return;
        if (!item.isRead) setUnread(unreadCount + 1);
        showLiveToast(item);
        if (dropdown && !dropdown.hidden) loadRecent();
      });
      connection.on("UnreadCountChanged", (count) => {
        if (Number.isFinite(count)) setUnread(count);
        if (dropdown && !dropdown.hidden) loadRecent();
      });
      connection.onreconnected(loadRecent);
      const start = async () => {
        try { await connection.start(); }
        catch { window.setTimeout(start, 5000); }
      };
      start();
    }
  }

  const previewVersion = document.querySelector("[data-preview-version]");
  const publicationPreview = document.querySelector("[data-publication-preview]");
  if (previewVersion && publicationPreview) {
    const updatePreview = () => {
      const option = previewVersion.selectedOptions[0];
      if (!option) return;
      const setText = (selector, value) => publicationPreview.querySelectorAll(selector).forEach((element) => { element.textContent = value; });
      setText("[data-preview-name]", option.dataset.name ?? "Proyecto");
      setText("[data-preview-career]", option.dataset.career ?? "Sin carrera");
      setText("[data-preview-subject]", option.dataset.subject ?? "Sin materia");
      setText("[data-preview-cycle]", option.dataset.cycle ?? "Sin ciclo");
      setText("[data-preview-type]", option.dataset.type ?? "Sin tipo");
      setText("[data-preview-development]", option.dataset.development === "2" ? "Terminado" : "En proceso");
      const initial = publicationPreview.querySelector("[data-preview-fallback] strong");
      if (initial) initial.textContent = (option.dataset.name ?? "P").trim().charAt(0).toLocaleUpperCase("es-MX") || "P";
    };
    previewVersion.addEventListener("change", updatePreview);
    document.querySelector("[data-preview-featured]")?.addEventListener("change", (event) => {
      const featured = event.currentTarget.checked;
      publicationPreview.classList.toggle("is-featured", featured);
      const badge = publicationPreview.querySelector("[data-preview-featured-badge]");
      if (badge) badge.hidden = !featured;
    });
    updatePreview();
  }
})();
