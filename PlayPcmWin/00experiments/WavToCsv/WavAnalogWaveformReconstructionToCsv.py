from scipy.io import wavfile
import argparse
import pathlib
import pandas as pd
import math
import numpy as np

parser = argparse.ArgumentParser()
parser.add_argument('inWavPath', type=pathlib.Path)
parser.add_argument('outCsvPath', type=pathlib.Path)
parser.add_argument('--filterSz', type=int, default=4095)
parser.add_argument('--magScale', type=int, default=6)
parser.add_argument('--saveStart', type=int, default=4095)
parser.add_argument('--saveSamples', type=int, default=50)
args = parser.parse_args()

samplerate, pcm = wavfile.read(args.inWavPath)

filterSz=args.filterSz
magScale=args.magScale

halfSz=filterSz//2


# filter coeffs
fc = []
for i in range(filterSz):
    x = i - halfSz
    xn = x * math.pi / magScale
    s = 1.0
    if x != 0:
        s = math.sin(xn) / xn
    
    h = (1.0 + math.cos(math.pi * x / halfSz) ) / 2.0
    
    r = s * h
    fc.append(r)

nfs = np.array(fc)

#print(nfs)

nPCM = np.array(pcm)

#print(nPCM.shape)

nLch = nPCM[:,0]

#print(nLch.shape)

#print(nfs.shape)

LchC2 = []
for s in range(nLch.shape[0]):
    v = nLch[s]
    for i in range(magScale):
        LchC2.append(v)

nLchC2 = np.array(LchC2)

#print(nLchC2.shape)

ncr = np.convolve(nLchC2, nfs, mode='same')

#print(ncr.shape)

ncrSave = ncr[args.saveStart:args.saveStart+args.saveSamples]

df = pd.DataFrame(ncrSave)
df.to_csv(args.outCsvPath, sep ='\t')

