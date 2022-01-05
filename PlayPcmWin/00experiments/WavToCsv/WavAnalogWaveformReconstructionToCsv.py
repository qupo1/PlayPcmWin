from scipy.io import wavfile
import argparse
import pathlib
import pandas as pd
import math
import numpy as np

parser = argparse.ArgumentParser()
parser.add_argument('inWavPath', type=pathlib.Path)
parser.add_argument('outCsvPath', type=pathlib.Path)

args = parser.parse_args()

samplerate, pcm = wavfile.read(args.inWavPath)

filterSz=4095
magScale=20

procSamples=50

halfSz=filterSz//2


# filter coeffs
fc = []
for i in range(filterSz):
    x = i - halfSz
    xn = x * 3.14159265 / magScale
    s = 1.0
    if x != 0:
        s = math.sin(xn) / xn
    
    h = (1.0 + math.cos(3.14159265 * x / halfSz) ) / 2.0
    
    r = s * h
    fc.append(r)

nfs = np.array(fc)

#print(nfs)

nPCM = np.array(pcm)

print(nPCM.shape)

nLch = nPCM[:,0]

print(nLch.shape)

print(nfs.shape)

LchC2 = []
for s in range(magScale * procSamples):
    v = nLch[s]
    for i in range(magScale):
        LchC2.append(v)

nLchC2 = np.array(LchC2)

print(nLchC2.shape)

ncr = np.convolve(nLchC2, nfs, mode='same')

print(ncr.shape)

df = pd.DataFrame(ncr)

df.to_csv(args.outCsvPath)
