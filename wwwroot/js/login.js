document.addEventListener("DOMContentLoaded", () => {
    const formLogin = document.querySelector("#login") || document.querySelector("#form-login");
    const feedbackLogin = document.querySelector(".feedback") || document.getElementById("feedback-login");

    if (formLogin) {
        formLogin.addEventListener("submit", async (e) => {
            e.preventDefault();
            
            if (feedbackLogin) {
                feedbackLogin.textContent = "";
            }

            try {
                const formData = new FormData(formLogin);
                const response = await fetch("/Auth/Login", {
                    method: "POST",
                    body: formData
                });
                
                if (response.ok) {
                    // Login exitoso: redirige directo al inicio de la app
                    window.location.href = "/";
                } else {
                    const errorText = await response.text();
                    if (feedbackLogin) {
                        feedbackLogin.textContent = errorText;
                    } else {
                        alert(errorText);
                    }
                }
            } catch (error) {
                const mensajeError = "Ocurrió un error al intentar iniciar sesión. Verificá tu conexión.";
                if (feedbackLogin) {
                    feedbackLogin.textContent = mensajeError;
                } else {
                    alert(mensajeError);
                }
            }
        });
    }
});