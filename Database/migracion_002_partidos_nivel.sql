-- Ejecutar una vez si ya se creó la tabla partidos con la versión anterior.
alter table public.partidos drop column if exists cupos_totales;
alter table public.partidos add column if not exists nivel text;
alter table public.partidos add column if not exists modalidad_padel text;
update public.partidos set nivel = 'Intermedio' where nivel is null;
alter table public.partidos alter column nivel set not null;
alter table public.partidos add constraint partidos_nivel_valido check (nivel in ('Principiante','Intermedio','Avanzado'));
alter table public.partidos add constraint partidos_modalidad_padel check ((deporte = 'Pádel' and modalidad_padel in ('Mixto','Hombre','Mujer')) or (deporte = 'Fútbol' and modalidad_padel is null));
