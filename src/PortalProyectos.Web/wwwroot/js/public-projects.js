(() => {
  "use strict";
  document.querySelectorAll("[data-like-form]").forEach((form) => {
    form.addEventListener("submit", async (event) => {
      event.preventDefault();
      const button = form.querySelector("button[type='submit']");
      if (!button || button.disabled) return;
      button.disabled = true;
      try {
        const token = form.querySelector("input[name='__RequestVerificationToken']")?.value;
        const response = await fetch(form.action, { method: "POST", headers: { "RequestVerificationToken": token ?? "", "X-Requested-With": "XMLHttpRequest" } });
        if (response.status === 401) { window.location.assign("/cuenta/iniciar-sesion"); return; }
        const payload = await response.json();
        if (!response.ok) throw new Error(payload.message ?? "No se pudo actualizar el like.");
        button.classList.toggle("is-liked", payload.liked);
        button.setAttribute("aria-label", payload.liked ? "Quitar me gusta" : "Me gusta");
        const icon = button.querySelector("span"); if (icon) icon.textContent = payload.liked ? "♥" : "♡";
        const count = button.querySelector("[data-like-count]"); if (count) count.textContent = payload.total;
      } catch (error) {
        window.alert(error instanceof Error ? error.message : "No se pudo actualizar el like.");
      } finally { button.disabled = false; }
    });
  });
  document.querySelectorAll(".image-drop-zone input[type='file']").forEach((input) => {
    input.addEventListener("change", () => {
      const file = input.files?.[0];
      if (!file) return;
      const validType = ["image/jpeg", "image/png", "image/webp"].includes(file.type);
      if (!validType || file.size > 5 * 1024 * 1024) {
        window.alert("Selecciona JPG, PNG o WebP de máximo 5 MB.");
        input.value = "";
        return;
      }
      const label = input.closest(".image-drop-zone")?.querySelector("strong");
      if (label) label.textContent = `${file.name} · ${(file.size / 1024 / 1024).toFixed(1)} MB`;
    });
  });
})();
