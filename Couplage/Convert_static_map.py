from collections import defaultdict
import sys
import csv
from pathlib import Path

def read_map(file_path):
    data = defaultdict(lambda: defaultdict(float))
    with open(file_path, "r", newline="") as f:
        for line_number, line in enumerate(f):
            if line.startswith("row"):
                continue

            parts = line.rstrip("\n").split(";")
            if len(parts) < 3:
                print(f"Skipping line {line_number}: {line}")
                continue

            row = int(parts[0])
            col = int(parts[1])
            value = float(parts[2])
            data[row][col] = value
    return data

def convert_static_map(file_path):
    data = read_map(file_path)
    all_rows = sorted(data.keys())
    all_cols = sorted({col for row_data in data.values() for col in row_data.keys()})

    file_path = Path(file_path)
    fileName = file_path.stem
    fileName = fileName.replace(" ", "_")
    print(f"File Name: {fileName}")
    csv_file_path = file_path.with_name(fileName + file_path.suffix)
    print(f"Renamed File Path: {file_path}")
    output_path = csv_file_path.with_suffix(".csv")

    with open(output_path, "w", newline="") as f:
        writer = csv.writer(f)
        writer.writerow([""] + [str(col) for col in all_cols])

        for row in all_rows:
            row_data = [str(row)]
            row_values = data[row]
            for col in all_cols:
                value = row_values.get(col, 0.0)
                row_data.append("0" if abs(value) < 1e-10 else value)
            writer.writerow(row_data)

        

def convert_static_maps(directory):
    directory = Path(directory)
    for file_path in directory.glob("*.txt"):
        convert_static_map(file_path)

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage:")
        print("  python Convert_static_map.py <fichier.txt>")
        print("  python Convert_static_map.py <dossier>")
        sys.exit(1)

    path = Path(sys.argv[1])
    if path.is_file():
        convert_static_map(path)
    elif path.is_dir():
        convert_static_maps(path)
    else:
        print(f"Chemin introuvable: {path}")
        sys.exit(2)