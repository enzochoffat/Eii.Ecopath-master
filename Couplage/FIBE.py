import sys
import csv

def Convert_biomass_map(biomass_map_path):
    biomass_map = {}

    with open(biomass_map_path, "r") as f:
        for line in f:
            if line.startswith("row"):
                continue

            parts = line.strip().split(";")
            if len(parts) < 4:
                continue

            if parts[2] == "1":
                row = int(parts[0])
                col = int(parts[1])
                biomass_map.setdefault(row, {})[col] = parts[3]

    print(len(biomass_map))
    Convert_biomass_map_to_csv(biomass_map)
    return biomass_map

def Convert_biomass_map_to_csv(biomass_map):
    all_row = sorted(biomass_map.keys())
    all_col = sorted({col for row in biomass_map.values() for col in row.keys()})
    with open("biomass_map.csv", "w") as f:
        writer = csv.writer(f)
        header = [""] + [str(c) for c in all_col]
        writer.writerow(header)
        for r in all_row:
            row_data = []
            row_data.append(str(r))
            for c in all_col:
                value = biomass_map[r].get(c, float(0))
                value = float(value)
                if value < 1e-10:
                    print(f"Value at row {r}, col {c} is {value}, setting to 0")
                    value = "0"
                row_data.append(value)
            writer.writerow(row_data)
    



if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python FIBE.py <chemin_du_fichier_txt>")
    else:
        Convert_biomass_map(sys.argv[1])