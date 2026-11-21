# Electiva 1 - Arquitectura y extracción ETL (.NET 8)

Proyecto: **Análisis de Opiniones de Clientes**. Entrega de la etapa **E (Extracción)** de ETL, desarrollada como Worker Service .NET 8. El código usa los seis CSV proporcionados: cuatro se extraen como archivos, `web_reviews.csv` se importa primero a SQL Server y `social_comments.csv` se expone mediante una API REST local real para que `HttpClient` la consuma.

## Estructura

- `OpinionesETL.sln`: solución para Visual Studio 2022 con tres proyectos .NET 8.
- `src/OpinionesETL.Worker`: extracción CSV + SQL + API, logs `ILogger`, escritura JSONL staging, cronómetro, paralelismo.
- `src/OpinionesETL.MockApi`: API REST local que publica los comentarios del CSV en páginas.
- `src/OpinionesETL.Seeder`: importa reseñas del CSV a SQL Server, evitando duplicados por `IdReview`.
- `Data/`: seis CSV originales.
- `sql/`: script de creación/verificación de la BD fuente relacional.
- `diagramas/`: arquitectura y flujo en PNG, SVG y DOT editable.
- `documentacion/`: informe técnico PDF.
- `evidencias/`: guía de capturas reales para demostrar la ejecución.
- `tools/verificar_staging.py`: comprobación opcional de los JSONL resultantes.

## Requisitos de instalación

- Windows 10/11, **Visual Studio 2022** con carga de trabajo «Desarrollo de escritorio de .NET» o SDK .NET 8.
- SQL Server (Developer/Express) y SQL Server Management Studio (SSMS).
- Acceso a NuGet para restaurar paquetes la primera vez.
- Python 3 (opcional, solamente para `tools/verificar_staging.py`).

## Pasos para ejecutar el proyecto

### 1. Abrir solución

Extrae el ZIP, abre `OpinionesETL.sln` en Visual Studio y espera la restauración de NuGet. Alternativamente abre PowerShell en la carpeta raíz:

```powershell
dotnet restore OpinionesETL.sln
```

### 2. Crear la base de datos fuente

En SSMS, conectado a tu instancia de SQL Server, ejecuta completo:

`sql/01_create_database_and_table.sql`

Esto crea **OpinionesFuentesDB** y `dbo.WebReviews`, sin tocar la base existente **AnalisisOpinionesDB**.

### 3. Configurar conexión SQL sin colocar contraseñas en el repositorio

Abre **PowerShell** y cambia el nombre de servidor si corresponde. Ejemplo para SQL Server Express con autenticación de Windows:

```powershell
$env:ConnectionStrings__SourceDatabase = 'Server=.\SQLEXPRESS;Database=OpinionesFuentesDB;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
```

Si la instancia es la predeterminada, usa `Server=.`. El ajuste `TrustServerCertificate=True` **solo está pensado para un SQL Server de desarrollo local**. Para producción usa certificados validados y HTTPS para la API.

Si utilizas credenciales SQL, establece el secreto en la variable de entorno (nunca dentro de `appsettings.json` ni lo subas a GitHub).

### 4. Sembrar las reseñas en SQL Server

En el **mismo PowerShell que contiene la variable de conexión**:

```powershell
dotnet run --project src/OpinionesETL.Seeder
```

Ejecuta luego `sql/02_verificar_fuente.sql` en SSMS. El conteo esperado es **200 reseñas**. Una segunda ejecución del Seeder no duplica los ID existentes.

### 5. Iniciar la API REST local (terminal 1)

```powershell
dotnet run --project src/OpinionesETL.MockApi
```

Abre en el navegador:

- `http://localhost:5075/health`
- `http://localhost:5075/api/comments?page=1&pageSize=100`

La API debe devolver JSON con `items` y `hasMore`. Mantén esta terminal abierta durante la extracción.

### 6. Ejecutar el Worker (terminal 2)

En una **nueva terminal PowerShell**, establece otra vez la variable de conexión del paso 3, porque las variables `$env:` no se comparten automáticamente entre ventanas, y ejecuta:

```powershell
dotnet run --project src/OpinionesETL.Worker
```

La consola registra eventos con `ILogger`, conteos y tiempos mediante `Stopwatch`. Los datos se escriben en:

`src/OpinionesETL.Worker/bin/Debug/net8.0/Staging/`

Se crean **6 archivos `.jsonl`** (uno por fuente), con 1 JSON por línea. `*.tmp` se elimina ante un fallo en esa fuente. Los archivos terminados no se pisan en nuevas ejecuciones.

| Fuente | Conteo esperado |
|---|---:|
| encuestas | 500 |
| clientes | 500 |
| productos | 200 |
| fuentes | 100 |
| resenas_sql | 200 |
| comentarios_api | 200 |
| **TOTAL** | **1,700** |

**Estos son conteos esperados a partir de los CSV, no resultados de una corrida verificada en tu SQL Server.** Para demostrar el resultado, captura la pantalla de la terminal de Visual Studio y verifica los archivos de staging tras ejecutar.

Opcional, para validar el JSONL con Python:

```powershell
python tools/verificar_staging.py src/OpinionesETL.Worker/bin/Debug/net8.0/Staging
```

## Resultados de ejecución

La solución se compiló en Visual Studio con tres proyectos correctos y cero errores.
El Worker Service en .NET 8 procesó seis fuentes y extrajo 1,700 registros.

| Fuente | Registros |
|---|---:|
| Encuestas CSV | 500 |
| Clientes CSV | 500 |
| Productos CSV | 200 |
| Catálogo de fuentes CSV | 100 |
| Reseñas desde SQL Server | 200 |
| Comentarios desde la API REST local | 200 |
| **Total** | **1,700** |

En la ejecución documentada, el Worker reportó un tiempo global de 467 ms.
Los registros se guardaron en seis archivos JSONL de staging.

El [informe técnico con evidencias](documentacion/Informe_Tecnico_Actividad1_ETL.pdf)
incluye los diagramas y las capturas de compilación, SQL Server, API REST,
ejecución del Worker y archivos de staging.

## Diseño técnico y alcance

- **Eficiencia:** lectura y escritura en streaming; `SqlDataReader` para SQL; páginas REST; tareas asíncronas y ejecución concurrente de tres extractores.
- **Escalabilidad:** fuentes CSV agregables desde `appsettings.json`; extractores desacoplados mediante `IExtractor`.
- **Seguridad:** cadena SQL mediante variable de entorno, consultas fijas y parametrización en el Seeder; datos locales por HTTP solo en modo de prueba.
- **Mantenibilidad:** clases separadas para extracción, opciones, modelos y almacenamiento.
- **Resiliencia:** archivos `.tmp` hasta cierre exitoso; manejo de errores y código de salida diferente de cero ante fallo; no borra staging anterior.
- **Observabilidad:** eventos, errores, conteos y tiempo total.

### Consideración de los IDs

`surveys_part1.csv` contiene ID de clientes/productos fuera del rango de `clients.csv`/`products.csv`; `web_reviews.csv` usa identificadores tipo `C007`/`P016`, mientras los catálogos usan enteros. La extracción **preserva exactamente cada ID original**. No se realizan relaciones, depuración, imputación ni transformaciones en esta actividad.

## Prueba parcial / diagnóstico

Si todavía no tienes SQL Server o no logras iniciar la API, puedes habilitar/deshabilitar fuentes con `Extraction:Database:Enabled` y `Extraction:Api:Enabled` en `src/OpinionesETL.Worker/appsettings.json` y ejecutar solo CSV. Vuelve a habilitar ambos para demostrar **los tres orígenes**, requisito de la práctica.

### Errores habituales

- `Login failed` / `Instance failure`: confirmar que SQL Server esté iniciado y el `Server=` correcto; probar conexión en SSMS.
- `Falta ConnectionStrings__SourceDatabase`: crear la variable en la misma terminal desde donde corre el Worker/Seeder.
- `Connection refused localhost:5075`: iniciar primero MockApi.
- `Could not find ...csv`: recompilar el proyecto y verificar que los CSV estén copiados en `bin/Debug/net8.0/Data/`.
- `SDK NET8 not found`: instalar .NET SDK 8 o el componente correspondiente de Visual Studio.

