#!/usr/bin/env python
# -*- coding: utf-8 -*-

"""Converts Ecospace biomass map .txt files (row;col;group;biomass) to .csv.

Each species (group) gets its own CSV file with rows as row indices and
columns as column indices, using parallel workers for speed.
"""

import csv
import os
import sys
from collections import defaultdict
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path
from typing import DefaultDict, Dict, Optional


def parse_biomass_map(
    biomass_map_path: str,
) -> DefaultDict[str, DefaultDict[int, Dict[int, float]]]:
    """Parse a biomass map text file into a nested dict.

    Args:
        biomass_map_path: Path to the biomass map .txt file.

    Returns:
        Nested dict: species_data[species][row][col] = biomass.
    """
    species_data: DefaultDict[
        str, DefaultDict[int, Dict[int, float]]
    ] = defaultdict(lambda: defaultdict(dict))
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


def write_species_csv(
    species: str,
    biomass_map: Dict[int, Dict[int, float]],
    output_dir: Path,
) -> None:
    """Write a single species biomass map to a CSV file.

    Args:
        species: Species (group) identifier used in the filename.
        biomass_map: Dict[ row ][ col ] = biomass.
        output_dir: Directory to write the CSV into.
    """
    all_rows = sorted(biomass_map.keys())
    all_cols = sorted(
        {col for row_data in biomass_map.values() for col in row_data.keys()}
    )
    output_path = output_dir / f"biomass_map_{species}.csv"
    with open(output_path, "w", newline="") as f:
        writer = csv.writer(f)
        writer.writerow([species])
        writer.writerow([""] + [str(col) for col in all_cols])
        for row in all_rows:
            row_values = biomass_map[row]
            row_data = [str(row)]
            for col in all_cols:
                value = row_values.get(col, 0.0)
                row_data.append("0" if abs(value) < 1e-10 else value)
            writer.writerow(row_data)


def convert_biomass_map(
    biomass_map_path: str,
    max_workers: Optional[int] = None,
) -> DefaultDict[str, DefaultDict[int, Dict[int, float]]]:
    """Convert a biomass map .txt to per-species CSV files.

    Args:
        biomass_map_path: Path to the input .txt file.
        max_workers: Maximum number of parallel workers (auto if None).

    Returns:
        Parsed species data dict.
    """
    path = Path(biomass_map_path)
    output_dir = path.parent.parent / "Biomass"
    species_data = parse_biomass_map(biomass_map_path)
    if max_workers is None:
        max_workers = min(32, (os.cpu_count() or 4) + 4)
    futures = []
    with ThreadPoolExecutor(max_workers=max_workers) as ex:
        for species, biomass_map in species_data.items():
            futures.append(
                ex.submit(write_species_csv, species, biomass_map, output_dir)
            )
        for future in as_completed(futures):
            try:
                future.result()
            except Exception as e:
                print(f"Error processing species: {e}")
    return species_data


def main() -> None:
    """Parse CLI arguments and run conversion."""
    if len(sys.argv) < 2:
        print(f"Usage: python {Path(__file__).name} <chemin_du_fichier_txt>")
        sys.exit(1)
    convert_biomass_map(sys.argv[1])


if __name__ == "__main__":
    main()
