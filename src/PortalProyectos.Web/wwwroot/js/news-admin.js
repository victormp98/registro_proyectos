(() => {
  "use strict";
  document.querySelectorAll(".image-drop-zone input[type='file']").forEach((input) => input.addEventListener("change", () => {
    const file = input.files?.[0]; if (!file) return;
    const validImage = ["image/jpeg", "image/png", "image/webp"].includes(file.type) && file.size <= 5 * 1024 * 1024;
    const maxVideoBytes = Number(input.dataset.maxBytes ?? 104857600);
    const validVideo = file.type === "video/mp4" && file.size <= maxVideoBytes;
    if (!(input.name === "image" ? validImage : validVideo)) {
      window.alert(input.name === "image" ? "Selecciona JPG, PNG o WebP de máximo 5 MB." : `Selecciona MP4 de máximo ${Math.floor(maxVideoBytes / 1024 / 1024)} MB.`);
      input.value = ""; return;
    }
    const label = input.closest(".image-drop-zone")?.querySelector("strong");
    if (label) label.textContent = `${file.name} · ${(file.size / 1024 / 1024).toFixed(1)} MB`;
  }));
  document.querySelectorAll("button[data-confirm]").forEach((button) => button.addEventListener("click", (event) => {
    if (!window.confirm(button.dataset.confirm ?? "¿Confirmas esta acción?")) event.preventDefault();
  }));
})();
