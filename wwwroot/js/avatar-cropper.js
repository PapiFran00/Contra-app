/* Cropper de avatar sin dependencias: salida JPEG cuadrada para Supabase Storage. */
(() => {
    const MAX_FILE_SIZE = 5 * 1024 * 1024;
    const OUTPUT_SIZE = 512;

    function createCropperDialog() {
        const dialog = document.createElement("dialog");
        dialog.className = "avatar-cropper-modal";
        dialog.innerHTML = `
            <form method="dialog" class="avatar-cropper-dialog" aria-labelledby="avatarCropperTitle">
                <header class="avatar-cropper-header">
                    <div><p class="page-kicker">FOTO DE PERFIL</p><h2 id="avatarCropperTitle">Encuadrá tu foto</h2><p>Arrastrá la imagen y usá el zoom para ajustarla.</p></div>
                    <button class="avatar-cropper-close" type="button" aria-label="Cerrar">×</button>
                </header>
                <div class="avatar-cropper-viewport"><canvas width="512" height="512" aria-label="Imagen a recortar"></canvas></div>
                <label class="avatar-cropper-zoom" for="avatarCropperZoom"><span>−</span><input id="avatarCropperZoom" type="range" min="1" max="3" value="1" step="0.01" aria-label="Nivel de zoom" /><span>+</span></label>
                <div class="avatar-cropper-actions"><button class="text-button" type="button" data-avatar-cancel>Cancelar</button><button class="button" type="button" data-avatar-confirm>Confirmar foto</button></div>
            </form>`;
        document.body.append(dialog);
        return dialog;
    }

    class AvatarCropper {
        constructor({ input, preview, feedback, onConfirm }) {
            this.input = input;
            this.preview = preview;
            this.feedback = feedback;
            this.onConfirm = onConfirm;
            this.dialog = createCropperDialog();
            this.canvas = this.dialog.querySelector("canvas");
            this.ctx = this.canvas.getContext("2d");
            this.zoom = this.dialog.querySelector("input[type=range]");
            this.objectUrl = null;
            this.image = null;
            this.drag = null;
            this.scaleBase = 1;
            this.x = 0;
            this.y = 0;
            this.bind();
        }

        bind() {
            this.input.addEventListener("change", () => this.loadFile(this.input.files?.[0]));
            this.zoom.addEventListener("input", () => this.draw());
            this.canvas.addEventListener("pointerdown", event => {
                if (!this.image) return;
                this.drag = { x: event.clientX, y: event.clientY, imageX: this.x, imageY: this.y };
                this.canvas.setPointerCapture(event.pointerId);
            });
            this.canvas.addEventListener("pointermove", event => {
                if (!this.drag) return;
                const factor = this.canvas.width / this.canvas.clientWidth;
                this.x = this.drag.imageX + (event.clientX - this.drag.x) * factor;
                this.y = this.drag.imageY + (event.clientY - this.drag.y) * factor;
                this.draw();
            });
            ["pointerup", "pointercancel", "lostpointercapture"].forEach(type => this.canvas.addEventListener(type, () => this.drag = null));
            this.dialog.querySelector(".avatar-cropper-close").addEventListener("click", () => this.close());
            this.dialog.querySelector("[data-avatar-cancel]").addEventListener("click", () => this.close());
            this.dialog.querySelector("[data-avatar-confirm]").addEventListener("click", () => this.confirm());
            this.dialog.addEventListener("click", event => { if (event.target === this.dialog) this.close(); });
            this.dialog.addEventListener("cancel", event => { event.preventDefault(); this.close(); });
        }

        loadFile(file) {
            if (!file) return;
            if (!/^image\/(jpeg|png|webp)$/i.test(file.type) || file.size > MAX_FILE_SIZE) {
                this.message("Elegí una imagen JPG, PNG o WebP de hasta 5 MB.");
                this.input.value = "";
                return;
            }
            if (this.objectUrl) URL.revokeObjectURL(this.objectUrl);
            this.objectUrl = URL.createObjectURL(file);
            const image = new Image();
            image.onload = () => {
                this.image = image;
                this.scaleBase = Math.max(this.canvas.width / image.naturalWidth, this.canvas.height / image.naturalHeight);
                this.zoom.value = "1";
                this.x = (this.canvas.width - image.naturalWidth * this.scaleBase) / 2;
                this.y = (this.canvas.height - image.naturalHeight * this.scaleBase) / 2;
                this.draw();
                this.dialog.showModal();
            };
            image.onerror = () => this.message("No pudimos abrir esa imagen. Probá con otro archivo.");
            image.src = this.objectUrl;
        }

        draw() {
            if (!this.image) return;
            const scale = this.scaleBase * Number(this.zoom.value);
            const width = this.image.naturalWidth * scale;
            const height = this.image.naturalHeight * scale;
            this.x = Math.max(Math.min(0, this.canvas.width - width), Math.min(0, this.x));
            this.y = Math.max(Math.min(0, this.canvas.height - height), Math.min(0, this.y));
            this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
            this.ctx.drawImage(this.image, this.x, this.y, width, height);
        }

        async confirm() {
            const blob = await new Promise(resolve => this.canvas.toBlob(resolve, "image/jpeg", 0.9));
            if (!blob) { this.message("No pudimos preparar la foto. Probá con otra imagen."); return; }
            const url = URL.createObjectURL(blob);
            this.preview.style.setProperty("--avatar-photo", `url("${url}")`);
            this.preview.classList.add("has-avatar");
            this.preview.querySelector("img")?.remove();
            this.onConfirm?.(blob, url);
            this.close();
        }

        close() {
            if (this.dialog.open) this.dialog.close();
            this.input.value = "";
        }

        message(text) { if (this.feedback) this.feedback.textContent = text; }
    }

    window.AvatarCropper = AvatarCropper;
    window.avatarOutputSize = OUTPUT_SIZE;
})();
