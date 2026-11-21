"""Validación estática de los seis CSV y archivos del proyecto (sin SDK .NET)."""
import csv
import json
import xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path

root = Path(__file__).resolve().parent.parent
expected = {
    'surveys_part1.csv': 500,
    'clients.csv': 500,
    'products.csv': 200,
    'fuente_datos.csv': 100,
    'web_reviews.csv': 200,
    'social_comments.csv': 200,
}
for filename, n in expected.items():
    with (root / 'Data' / filename).open(newline='', encoding='utf-8-sig') as f:
        rows = list(csv.DictReader(f))
    assert len(rows) == n, (filename, len(rows), n)
    print(f'OK CSV {filename}: {n} filas')

settings = json.loads((root/'src/OpinionesETL.Worker/appsettings.json').read_text(encoding='utf-8'))
assert len(settings['Extraction']['CsvSources']) == 4
assert settings['Extraction']['Database']['Enabled']
assert settings['Extraction']['Api']['Enabled']
for project in (root/'src').glob('*/*.csproj'):
    ET.parse(project)
    print('OK XML', project.relative_to(root))

for fname in ['OpinionesETL.sln','README.md','sql/01_create_database_and_table.sql',
              'diagramas/arquitectura.png','diagramas/flujo_etl.png',
              'documentacion/Informe_Tecnico_Actividad1_ETL.pdf']:
    assert (root/fname).is_file(), fname

print('Validación estática completada. Total de filas fuente:',sum(expected.values()))
print('NO valida compilación ni ejecución integrada: requiere SDK .NET 8 y SQL Server.')
