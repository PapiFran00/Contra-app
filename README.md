# CONTRA APP

Aplicación móvil web basada en ASP.NET Core MVC (.NET 8) y Supabase. No contiene datos de demostración: todas las listas se consultan en la base configurada.

## Puesta en marcha

1. Crear un proyecto de Supabase y ejecutar [Database/schema.sql](Database/schema.sql). Si ya aplicaste la versión previa, ejecutar después `Database/migracion_002_partidos_nivel.sql`, `Database/migracion_003_comunidad.sql` y [Database/migracion_004_avatars.sql](Database/migracion_004_avatars.sql).
2. Configurar la URL y la `AnonKey` mediante secretos: `dotnet user-secrets set "Supabase:Url" "..."` y `dotnet user-secrets set "Supabase:AnonKey" "..."`.
3. Configurar `Supabase:ServiceRoleKey` exclusivamente en secretos o variables de producción: `dotnet user-secrets set "Supabase:ServiceRoleKey" "TU_SERVICE_ROLE_KEY"`. Para producción, usar la variable `Supabase__ServiceRoleKey`. Nunca copiarla a `wwwroot` ni al cliente. La clave se obtiene en Supabase: **Project Settings → API → service_role**.
4. En producción definir `Supabase__PublicAppUrl=https://TU-DOMINIO` (sin barra final). En **Supabase Auth → URL Configuration**, establecer ese dominio como **Site URL** y permitir `https://TU-DOMINIO/Auth/Confirmado` y `https://TU-DOMINIO/Auth/Login` como **Redirect URLs** (también sus equivalentes locales). El registro y la recuperación construyen estas rutas de forma dinámica; nunca quedan atados a `localhost:5000`.
5. Crear el primer usuario mediante Supabase Auth, insertar su perfil y promoverlo a `dueno` usando la instrucción indicada en el esquema.
6. Ejecutar `dotnet run`.

Las tablas deben tener Realtime habilitado; el script ya las agrega a la publicación. El avatar se recorta a 512×512 en el navegador, se guarda como JPEG en el bucket público `avatars/<auth-user-id>/avatar.jpg` y su URL se persiste en `usuarios.avatar_url`. Con la confirmación de correo activada, `Supabase__ServiceRoleKey` es necesaria en el servidor para crear ese perfil y avatar antes de que exista una sesión; nunca debe exponerse al navegador. Para producción, publicar con HTTPS y establecer los secretos en el host.

## Logo oficial

La cabecera ya concentra la identidad visual en `Views/Shared/_Layout.cshtml`. Para usar el archivo oficial exactamente como corresponde, agregá el recurso original como `wwwroot/images/logo-contra.svg` o `.png` y reemplazá el bloque `brand-mark` por la etiqueta `<img>` correspondiente; no se reconstruyó ni extrajo el logo desde las capturas para evitar degradar la marca.
