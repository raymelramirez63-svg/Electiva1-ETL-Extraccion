# Capturas sugeridas para completar la evidencia real de ejecución

Toma las capturas **después de ejecutar** la solución en Visual Studio y SQL Server. La documentación ya incluye evidencia del **código fuente** y diagramas; las siguientes capturas añaden una demostración práctica verificable.

1. **Solución de Visual Studio**: explorador mostrando `OpinionesETL.Worker`, `OpinionesETL.MockApi`, `OpinionesETL.Seeder` y sus archivos principales.
2. **SQL Server**: consulta `SELECT COUNT(*) FROM dbo.WebReviews`, con resultado 200.
3. **API REST**: navegador mostrando `http://localhost:5075/api/comments?page=1&pageSize=100` y JSON con `items` y `hasMore`.
4. **Worker en ejecución**: salida con las seis fuentes, conteos, tiempo y `EXTRACCIÓN COMPLETADA`.
5. **Staging**: carpeta con seis `.jsonl` y el contenido de un archivo (JSON por línea).
6. **Parcialmente opcional**: volver a ejecutar Seeder y confirmar 200 reseñas sin duplicados.

Inserta cada captura en el informe Word, junto a un pie de figura explicando **qué prueba**: SQL confirma la fuente relacional; JSON confirma el consumo HTTP; consola confirma el trabajo del Worker; `.jsonl` confirma persistencia temporal. No presentes los 1,700 registros como extraídos hasta que hayas ejecutado y verificado el Worker.
