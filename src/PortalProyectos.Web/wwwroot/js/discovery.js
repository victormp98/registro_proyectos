(() => {
  "use strict";
  const motion = window.matchMedia("(prefers-reduced-motion: reduce)");
  document.querySelectorAll("[data-project-rail]").forEach((rail) => {
    const track = rail.querySelector("[data-rail-track]");
    const controls = rail.querySelector("[data-rail-controls]");
    if (!track || !controls) return;
    const previous = controls.querySelector("[data-rail-prev]");
    const next = controls.querySelector("[data-rail-next]");
    const update = () => {
      const maximum = track.scrollWidth - track.clientWidth;
      controls.hidden = maximum < 2;
      previous.disabled = track.scrollLeft <= 2;
      next.disabled = track.scrollLeft >= maximum - 2;
    };
    const move = (direction) => {
      const first = track.firstElementChild;
      const gap = Number.parseFloat(getComputedStyle(track).columnGap) || 0;
      const step = first ? first.getBoundingClientRect().width + gap : track.clientWidth;
      track.scrollBy({ left: direction * step, behavior: motion.matches ? "instant" : "smooth" });
    };
    previous.addEventListener("click", () => move(-1));
    next.addEventListener("click", () => move(1));
    track.addEventListener("keydown", (event) => {
      if (event.target !== track) return;
      if (event.key === "ArrowLeft" || event.key === "ArrowRight") {
        event.preventDefault();
        move(event.key === "ArrowRight" ? 1 : -1);
      } else if (event.key === "Home" || event.key === "End") {
        event.preventDefault();
        track.scrollTo({ left: event.key === "Home" ? 0 : track.scrollWidth, behavior: "instant" });
      }
    });
    track.addEventListener("scroll", update, { passive: true });
    if ("ResizeObserver" in window) new ResizeObserver(update).observe(track);
    else window.addEventListener("resize", update);
    update();
  });
  const search = document.querySelector(".header-search");
  if (search) {
    search.addEventListener("toggle", () => {
      if (search.open) search.querySelector("input")?.focus();
    });
    document.addEventListener("keydown", (event) => {
      if (event.key === "Escape" && search.open) {
        search.open = false;
        search.querySelector("summary")?.focus();
      }
    });
    document.addEventListener("click", (event) => {
      if (search.open && !search.contains(event.target)) search.open = false;
    });
  }
})();
