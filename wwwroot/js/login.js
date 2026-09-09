const formLogin = document.querySelector("#form-login"); // o el selector de tu form de login
if (formLogin) {
    formLogin.addEventListener("submit", async (e) => {
        e.preventDefault();
        const formData = new FormData(formLogin);
        const response = await fetch("/Auth/Login", {
            method: "POST",
            body: formData
        });
        
        if (response.ok) {
            window.location.href = "/"; // Te lleva directo a la app logueado
        } else {
            const errorText = await response.text();
            alert(errorText); // Muestra el error si las credenciales fallan
        }
    });
}