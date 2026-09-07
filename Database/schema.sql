-- Ejecutar en el SQL Editor de Supabase antes de iniciar la aplicación.
create type public.rol_usuario as enum ('dueno', 'admin_complejo', 'jugador');
create type public.deporte as enum ('Fútbol', 'Pádel');
create type public.estado_partido as enum ('publicado', 'cancelado', 'finalizado');
create type public.estado_postulacion as enum ('pendiente', 'aceptada', 'rechazada');

create table public.usuarios (
  id uuid primary key references auth.users(id) on delete cascade,
  nombre text not null check (char_length(nombre) between 2 and 80), apellido text,
  fecha_nacimiento date, genero text, rol public.rol_usuario not null default 'jugador', avatar_url text,
  creado_en timestamptz not null default now()
);
create table public.complejos (
  id uuid primary key default gen_random_uuid(), administrador_id uuid not null unique references public.usuarios(id),
  nombre text not null unique, ciudad text not null default 'Sunchales', horario_apertura time, horario_cierre time,
  activo boolean not null default true, creado_en timestamptz not null default now()
);
create table public.canchas (
  id uuid primary key default gen_random_uuid(), complejo_id uuid not null references public.complejos(id) on delete cascade,
  nombre text not null, deporte public.deporte not null, techada boolean not null default false, activa boolean not null default true,
  unique(complejo_id,nombre)
);
create table public.partidos (
  id uuid primary key default gen_random_uuid(), creador_id uuid not null references public.usuarios(id),
  complejo_id uuid not null references public.complejos(id), cancha_id uuid not null references public.canchas(id),
  deporte public.deporte not null, fecha_hora timestamptz not null, nivel text not null check(nivel in ('Principiante','Intermedio','Avanzado')), modalidad_padel text check((deporte = 'Pádel' and modalidad_padel in ('Mixto','Hombre','Mujer')) or (deporte = 'Fútbol' and modalidad_padel is null)),
  estado public.estado_partido not null default 'publicado', notas text check(char_length(coalesce(notas,'')) <= 300), creado_en timestamptz not null default now()
);
create table public.postulaciones (
  id uuid primary key default gen_random_uuid(), partido_id uuid not null references public.partidos(id) on delete cascade,
  jugador_id uuid not null references public.usuarios(id), estado public.estado_postulacion not null default 'pendiente',
  creado_en timestamptz not null default now(), unique(partido_id,jugador_id)
);
create table public.conversaciones (id uuid primary key default gen_random_uuid(), partido_id uuid not null unique references public.partidos(id) on delete cascade, creado_en timestamptz not null default now());
create table public.mensajes (id uuid primary key default gen_random_uuid(), conversacion_id uuid not null references public.conversaciones(id) on delete cascade, remitente_id uuid not null references public.usuarios(id), contenido text not null check(char_length(contenido) between 1 and 1000), creado_en timestamptz not null default now());

create index on public.partidos(estado, fecha_hora); create index on public.canchas(complejo_id,deporte); create index on public.mensajes(conversacion_id,creado_en);

-- El primer usuario dueño se debe promover manualmente una sola vez, con su UUID real:
-- update public.usuarios set rol='dueno' where id='UUID_DEL_DUENO';
create or replace function public.es_participante(p_partido uuid) returns boolean language sql stable security definer set search_path=public as $$
 select exists(select 1 from partidos p where p.id=p_partido and p.creador_id=auth.uid()) or exists(select 1 from postulaciones x where x.partido_id=p_partido and x.jugador_id=auth.uid() and x.estado='aceptada'); $$;
create or replace function public.crear_conversacion_al_aceptar() returns trigger language plpgsql security definer set search_path=public as $$ begin if new.estado='aceptada' and old.estado is distinct from 'aceptada' then insert into conversaciones(partido_id) values(new.partido_id) on conflict do nothing; end if; return new; end $$;
create trigger postulacion_aceptada after update of estado on public.postulaciones for each row execute procedure public.crear_conversacion_al_aceptar();

create or replace function public.regenerar_canchas(p_complejo_id uuid,p_futbol int,p_futbol_techadas int,p_padel int,p_padel_techadas int) returns void language plpgsql security definer set search_path=public as $$
begin
 if not exists(select 1 from complejos where id=p_complejo_id and administrador_id=auth.uid()) then raise exception 'No autorizado'; end if;
 if exists(select 1 from partidos where cancha_id in(select id from canchas where complejo_id=p_complejo_id) and fecha_hora > now() and estado='publicado') then raise exception 'No se pueden regenerar canchas con partidos futuros publicados'; end if;
 delete from canchas where complejo_id=p_complejo_id;
 insert into canchas(complejo_id,nombre,deporte,techada)
 select p_complejo_id,'Fútbol '||n,'Fútbol',n<=p_futbol_techadas from generate_series(1,p_futbol)n;
 insert into canchas(complejo_id,nombre,deporte,techada)
 select p_complejo_id,'Pádel '||n,'Pádel',n<=p_padel_techadas from generate_series(1,p_padel)n;
end $$;

alter table public.usuarios enable row level security; alter table public.complejos enable row level security; alter table public.canchas enable row level security; alter table public.partidos enable row level security; alter table public.postulaciones enable row level security; alter table public.conversaciones enable row level security; alter table public.mensajes enable row level security;
create policy "perfil propio" on usuarios for all using(id=auth.uid()) with check(id=auth.uid());
create policy "leer complejos" on complejos for select using(activo or administrador_id=auth.uid());
create policy "editar complejo propio" on complejos for update using(administrador_id=auth.uid());
create policy "leer canchas" on canchas for select using(true);
create policy "leer partidos publicados" on partidos for select using(estado='publicado' or creador_id=auth.uid() or es_participante(id));
create policy "crear partido propio" on partidos for insert with check(creador_id=auth.uid());
create policy "leer postulacion propia o creador" on postulaciones for select using(jugador_id=auth.uid() or exists(select 1 from partidos where id=partido_id and creador_id=auth.uid()));
create policy "crear postulacion propia" on postulaciones for insert with check(jugador_id=auth.uid());
create policy "resolver postulacion creador" on postulaciones for update using(exists(select 1 from partidos where id=partido_id and creador_id=auth.uid()));
create policy "leer conversaciones propias" on conversaciones for select using(es_participante(partido_id));
create policy "leer mensajes propios" on mensajes for select using(exists(select 1 from conversaciones c where c.id=conversacion_id and es_participante(c.partido_id)));
create policy "enviar mensaje propio" on mensajes for insert with check(remitente_id=auth.uid() and exists(select 1 from conversaciones c where c.id=conversacion_id and es_participante(c.partido_id)));
alter publication supabase_realtime add table public.partidos, public.postulaciones, public.conversaciones, public.mensajes;
grant execute on function public.regenerar_canchas(uuid,int,int,int,int) to authenticated;
