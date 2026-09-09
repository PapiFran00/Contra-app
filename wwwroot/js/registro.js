const registro = document.querySelector("#registro");
const feedbackRegistro = registro.querySelector(".feedback");
const campo = nombre => registro.elements.namedItem(nombre);

const avatarInput = document.querySelector("#registrationAvatarInput");
const avatarButton = document.querySelector("#registrationAvatarButton");
const cropper = document.querySelector("#registrationCropper");
const cropperCanvas = document.querySelector("#registrationCropperCanvas");
const cropperContext = cropperCanvas.getContext("2d");
const cropperZoom = document.querySelector("#registrationCropperZoom");
let croppedAvatar = null, sourceImage = null, sourceUrl = null, baseScale = 1, position = { x: 0, y: 0 }, drag = null;

function drawCropper() {
    if (!sourceImage) return;
    cropperZoom.style.setProperty("--zoom-progress", `${((Number(cropperZoom.value) - 1) / 2) * 100}%`);
    const scale = baseScale * Number(cropperZoom.value);
    const width = sourceImage.naturalWidth * scale, height = sourceImage.naturalHeight * scale;
    position.x = Math.max(Math.min(0, cropperCanvas.width - width), Math.min(0, position.x));
    position.y = Math.max(Math.min(0, cropperCanvas.height - height), Math.min(0, position.y));
    cropperContext.clearRect(0, 0, cropperCanvas.width, cropperCanvas.height);
    cropperContext.drawImage(sourceImage, position.x, position.y, width, height);
}

function closeCropper() {
    drag = null;
    if (cropper.open) cropper.close();
    avatarInput.value = "";
}

avatarInput.addEventListener("change", () => {
    const file = avatarInput.files?.[0];
    if (!file) return;
    if (!/^image\/(jpeg|png|webp)$/i.test(file.type) || file.size > 5 * 1024 * 1024) {
        feedbackRegistro.textContent = "Elegí una imagen JPG, PNG o WebP de hasta 5 MB.";
        avatarInput.value = "";
        return;
    }
    if (sourceUrl) URL.revokeObjectURL(sourceUrl);
    sourceUrl = URL.createObjectURL(file);
    sourceImage = new Image();
    sourceImage.onload = () => {
        baseScale = Math.max(cropperCanvas.width / sourceImage.naturalWidth, cropperCanvas.height / sourceImage.naturalHeight);
        cropperZoom.value = "1";
        position = { x: (cropperCanvas.width - sourceImage.naturalWidth * baseScale) / 2, y: (cropperCanvas.height - sourceImage.naturalHeight * baseScale) / 2 };
        drawCropper();
        cropper.showModal();
    };
    sourceImage.onerror = () => feedbackRegistro.textContent = "No pudimos abrir esa imagen. Probá con otro archivo.";
    sourceImage.src = sourceUrl;
});

cropperZoom.addEventListener("input", drawCropper);
cropperCanvas.addEventListener("pointerdown", event => {
    if (!sourceImage) return;
    drag = { pointerX: event.clientX, pointerY: event.clientY, imageX: position.x, imageY: position.y };
    cropperCanvas.setPointerCapture(event.pointerId);
});
cropperCanvas.addEventListener("pointermove", event => {
    if (!drag) return;
    const canvasRatio = cropperCanvas.width / cropperCanvas.clientWidth;
    position.x = drag.imageX + (event.clientX - drag.pointerX) * canvasRatio;
    position.y = drag.imageY + (event.clientY - drag.pointerY) * canvasRatio;
    drawCropper();
});
["pointerup", "pointercancel", "lostpointercapture"].forEach(eventName => cropperCanvas.addEventListener(eventName, () => drag = null));

document.querySelector("#registrationCropperClose").addEventListener("click", closeCropper);
document.querySelector("#registrationCropperCancel").addEventListener("click", closeCropper);
cropper.addEventListener("cancel", event => { event.preventDefault(); closeCropper(); });
cropper.addEventListener("click", event => { if (event.target === cropper) closeCropper(); });
document.querySelector("#registrationCropperConfirm").addEventListener("click", async () => {
    const blob = await new Promise(resolve => cropperCanvas.toBlob(resolve, "image/jpeg", 0.9));
    if (!blob) { feedbackRegistro.textContent = "No pudimos preparar la foto. Probá con otra imagen."; return; }
    croppedAvatar = blob;
    avatarButton.style.setProperty("--registration-avatar-photo", `url("${URL.createObjectURL(blob)}")`);
    avatarButton.classList.add("has-photo");
    closeCropper();
});

registro.addEventListener("submit", async event => {
    event.preventDefault();
    feedbackRegistro.textContent = "";
    const correo = campo("correo"), contrasena = campo("contrasena"), confirmarContrasena = campo("confirmarContrasena");
    if (!correo.checkValidity()) { correo.reportValidity(); return; }
    if (contrasena.value !== confirmarContrasena.value) { feedbackRegistro.textContent = "Las contraseñas no coinciden."; return; }
    try {
        const body = new FormData(registro);
        if (croppedAvatar) body.append("avatar", croppedAvatar, "avatar.jpg");
        const response = await fetch("/Auth/Registro", { method: "POST", body });
        if (!response.ok) throw new Error(await response.text());
        
        // Éxito: avisamos y redirigimos al login
        alert("¡Cuenta creada con éxito! Ya podés iniciar sesión.");
        location.href = "/Auth/Login";
    } catch (error) { feedbackRegistro.textContent = error.message; }
});