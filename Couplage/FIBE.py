import sys
import csv
import os
from collections import defaultdict
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor, as_completed

def parse_biomass_map(biomass_map_path):
    species_data = defaultdict(lambda: defaultdict(dict))

    with open(biomass_map_path, "r", encoding="utf-8") as f:
        for line_number, line in enumerate(f): 
            clean_line = line.strip()

            if line_number == 0 and clean_line.startswith("row"):
                continue
            
            if not clean_line:
                continue

            parts = clean_line.split(";")
            if len(parts) < 4:
                continue

            row = int(parts[0])
            col = int(parts[1])
            species = parts[2]
            biomass_str = parts[3].replace(",", ".")
            biomass = float(biomass_str)

            species_data[species][row][col] = biomass

    return species_data


def write_species_csv(species, biomass_map, output_dir):
    all_rows = sorted(biomass_map.keys())
    all_cols = sorted({col for row_data in biomass_map.values() for col in row_data.keys()})

    output_path = output_dir / f"biomass_map_{species}.csv"

    with open(output_path, "w", newline="") as f:
        writer = csv.writer(f)
        writer.writerow([species])
        writer.writerow([""] + [str(col) for col in all_cols])

        for row in all_rows:
            row_data = [str(row)]
            row_values = biomass_map[row]
            for col in all_cols:
                value = row_values.get(col, 0.0)
                row_data.append("0" if abs(value) < 1e-10 else value)
            writer.writerow(row_data)


def convert_biomass_map(biomass_map_path, max_workers=None):
    biomass_map_path = Path(biomass_map_path)
    output_dir = biomass_map_path.parent.parent / "Biomass"

    species_data = parse_biomass_map(biomass_map_path)

    if max_workers is None:
        max_workers =min(32, (os.cpu_count() or 4) + 4)

    futures = []
    with ThreadPoolExecutor(max_workers=max_workers) as ex:
        for species, biomass_map in species_data.items():
            futures.append(ex.submit(write_species_csv, species, biomass_map, output_dir))

        for f in as_completed(futures):
            try:
                f.result()
            except Exception as e:
                print(f"Error processing species: {e}")

    return species_data
            


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python FIBE.py <chemin_du_fichier_txt>")
    else:
        convert_biomass_map(sys.argv[1])
