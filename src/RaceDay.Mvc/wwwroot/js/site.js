// Small progressive enhancements. Every page also works without JavaScript.

// Ask before destructive actions: <form data-confirm="Delete this event?">
document.addEventListener("submit", (e) => {
    const message = e.target.getAttribute("data-confirm");
    if (message && !window.confirm(message)) {
        e.preventDefault();
    }
});

// Live preview for image file inputs: <input type="file" data-preview="#imgId">
document.addEventListener("change", (e) => {
    const input = e.target;
    if (!(input instanceof HTMLInputElement) || input.type !== "file" || !input.dataset.preview) return;
    const preview = document.querySelector(input.dataset.preview);
    const file = input.files && input.files[0];
    if (preview && file && file.type.startsWith("image/")) {
        preview.src = URL.createObjectURL(file);
        preview.classList.remove("d-none");
    }
});

// Category form: only show the age fields for age categories.
document.addEventListener("change", (e) => {
    if (e.target.id !== "Form_CategoryType") return;
    toggleAgeFields(e.target.value);
});
function toggleAgeFields(type) {
    document.querySelectorAll("[data-age-field]").forEach((el) => el.classList.toggle("d-none", type !== "Age"));
}
document.addEventListener("DOMContentLoaded", () => {
    const select = document.getElementById("Form_CategoryType");
    if (select) toggleAgeFields(select.value);
});
