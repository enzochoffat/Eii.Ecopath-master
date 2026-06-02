import sys
import csv
from pathlib import Path

def convert_static_map(file_path):
    file_path = Path(file_path)
    output_path = file_path.with_suffix(".csv")

    with open(file_path, "r", newline="") as f_in, open(output_path, "w", newline="") as f_out:
        writer = csv.writer(f_out)

        first_data_written = False
        for line_number, line in enumerate(f_in):
            if line_number == 0 and line.startswith("row"):
                continue

            parts = line.rstrip("\n").split(";")
            if len(parts) < 4:
                continue

            row = int(parts[0])
            col = int(parts[1])
            value = float(parts[2])

            if not first_data_written:
                writer.writerow([file_path.stem])
                writer.writerow(["", col])
                first_data_written = True

            writer.writerow([row, value])

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