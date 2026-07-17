#!/usr/bin/env python
# -*- coding: utf-8 -*-

"""Converts Ecospace off-vessel price .txt files to .csv format.

Reads a semicolon-delimited text file with columns (fleet; group; price)
and writes a CSV with groups as rows and fleets as columns.
"""

from collections import defaultdict
import csv
import sys
from pathlib import Path
from typing import DefaultDict


def read_off_vessel_price(file_path: str) -> DefaultDict[int, DefaultDict[int, float]]:
    """Parse off-vessel price text file into a nested dict.

    Args:
        file_path: Path to the input .txt file.

    Returns:
        Nested dict: data[group][fleet] = price.
    """
    data: DefaultDict[int, DefaultDict[int, float]] = defaultdict(
        lambda: defaultdict(float)
    )
    with open(file_path, "r", newline="") as f:
        for line_number, line in enumerate(f):
            if line.startswith("fleet"):
                continue
            parts = line.rstrip("\n").split(";")
            if len(parts) < 3:
                print(f"Skipping line {line_number}: {line}")
                continue
            fleet = int(parts[0])
            group = int(parts[1])
            value = float(parts[2])
            data[group][fleet] = value
    return data


def convert_off_vessel_price(file_path: str) -> None:
    """Convert a single off-vessel price .txt to .csv.

    The output CSV has group numbers as rows and fleet numbers as columns.

    Args:
        file_path: Path to the input .txt file.
    """
    data = read_off_vessel_price(file_path)
    all_groups = sorted(data.keys())
    all_fleets = sorted(
        {col for row_data in data.values() for col in row_data.keys()}
    )
    path = Path(file_path)
    output_path = path.with_suffix(".csv")
    with open(output_path, "w", newline="") as f:
        writer = csv.writer(f)
        writer.writerow([""] + [str(fleet) for fleet in all_fleets])
        for group in all_groups:
            row_data = [str(group)]
            row_values = data[group]
            for fleet in all_fleets:
                value = row_values.get(fleet, 0.0)
                row_data.append("0" if abs(value) < 1e-10 else value)
            writer.writerow(row_data)
    print(f"Converted: {path} -> {output_path}")


def convert_all(directory: str) -> None:
    """Convert all .txt files in a directory.

    Args:
        directory: Path to the directory containing .txt files.
    """
    dir_path = Path(directory)
    for file_path in dir_path.glob("*.txt"):
        convert_off_vessel_price(str(file_path))


def main() -> None:
    """Parse CLI arguments and run conversion."""
    if len(sys.argv) < 2:
        print("Usage:")
        print(f"  python {Path(__file__).name} <fichier.txt>")
        print(f"  python {Path(__file__).name} <dossier>")
        sys.exit(1)
    path = Path(sys.argv[1])
    if path.is_file():
        convert_off_vessel_price(str(path))
    elif path.is_dir():
        convert_all(str(path))
    else:
        print(f"Chemin introuvable: {path}")
        sys.exit(2)


if __name__ == "__main__":
    main()
