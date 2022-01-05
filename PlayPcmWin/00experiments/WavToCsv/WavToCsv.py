from scipy.io import wavfile
import argparse
import pathlib
import csv 

parser = argparse.ArgumentParser()
parser.add_argument('inWavPath', type=pathlib.Path)
parser.add_argument('outCsvPath', type=pathlib.Path)

args = parser.parse_args()

samplerate, pcm = wavfile.read(args.inWavPath)

with open(args.outCsvPath, 'w', newline='') as f:
    w = csv.writer(f)
    w.writerows(pcm)
