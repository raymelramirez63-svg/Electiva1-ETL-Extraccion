"""Ejecute: python tools/verificar_staging.py RUTA_DE_STAGING.
Verifica el número de líneas JSONL y muestra los contadores por fuente.
"""
import json
import sys
from collections import Counter
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("Uso: python tools/verificar_staging.py <directorio_staging>")
folder = Path(sys.argv[1])
if not folder.is_dir():
    raise SystemExit(f"No existe el directorio: {folder}")
counts = Counter()
for path in folder.glob("*.jsonl"):
    n = 0
    with path.open(encoding="utf-8") as f:
        for line in f:
            payload = json.loads(line)
            assert "Source" in payload and "Fields" in payload and "SourceType" in payload
            counts[payload["Source"]] += 1
            n += 1
    print(f"{path.name}: {n} registros")
print("TOTAL:", sum(counts.values()))
print("FUENTES:", dict(counts))
