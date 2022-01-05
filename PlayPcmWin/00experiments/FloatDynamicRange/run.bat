mkdir -p tmp
FloatDynamicRange\FloatDynamicRange -generate32 24 tmp\original.bin

FloatDynamicRange\FloatDynamicRange -convert32 24 tmp\original.bin 1 1048576 tmp\M120dB.bin
FloatDynamicRange\FloatDynamicRange -convert32 24 tmp\M120dB.bin   1 1048576 tmp\M241dB.bin
FloatDynamicRange\FloatDynamicRange -convert32 24 tmp\M241dB.bin   1 1048576 tmp\M361dB.bin
FloatDynamicRange\FloatDynamicRange -convert32 24 tmp\M361dB.bin   1 1048576 tmp\M482dB.bin
FloatDynamicRange\FloatDynamicRange -convert32 24 tmp\M482dB.bin   1 1048576 tmp\M602dB.bin
FloatDynamicRange\FloatDynamicRange -convert32 24 tmp\M602dB.bin   1 1048576 tmp\M722dB.bin
FloatDynamicRange\FloatDynamicRange -convert32 24 tmp\M722dB.bin   1 1048576 tmp\M843dB.bin
FloatDynamicRange\FloatDynamicRange -convert32 24 tmp\M843dB.bin   1 1048576 tmp\M963dB.bin
FloatDynamicRange\FloatDynamicRange -convert32 24 tmp\M963dB.bin   1 1048576 tmp\M1084dB.bin

FloatDynamicRange\FloatDynamicRange -convert32 24 tmp\M722dB.bin        1048576 1 tmp\M722dBP120dB.bin
FloatDynamicRange\FloatDynamicRange -convert32 24 tmp\M722dBP120dB.bin  1048576 1 tmp\M722dBP241dB.bin
FloatDynamicRange\FloatDynamicRange -convert32 24 tmp\M722dBP241dB.bin  1048576 1 tmp\M722dBP361dB.bin
FloatDynamicRange\FloatDynamicRange -convert32 24 tmp\M722dBP361dB.bin  1048576 1 tmp\M722dBP482dB.bin
FloatDynamicRange\FloatDynamicRange -convert32 24 tmp\M722dBP482dB.bin  1048576 1 tmp\M722dBP602dB.bin
FloatDynamicRange\FloatDynamicRange -convert32 24 tmp\M722dBP602dB.bin  1048576 1 tmp\M722dBP722dB.bin

FloatDynamicRange\FloatDynamicRange -convert32 24 tmp\M843dB.bin        1048576 1 tmp\M843dBP120dB.bin
FloatDynamicRange\FloatDynamicRange -convert32 24 tmp\M843dBP120dB.bin  1048576 1 tmp\M843dBP241dB.bin
FloatDynamicRange\FloatDynamicRange -convert32 24 tmp\M843dBP241dB.bin  1048576 1 tmp\M843dBP361dB.bin
FloatDynamicRange\FloatDynamicRange -convert32 24 tmp\M843dBP361dB.bin  1048576 1 tmp\M843dBP482dB.bin
FloatDynamicRange\FloatDynamicRange -convert32 24 tmp\M843dBP482dB.bin  1048576 1 tmp\M843dBP602dB.bin
FloatDynamicRange\FloatDynamicRange -convert32 24 tmp\M843dBP602dB.bin  1048576 1 tmp\M843dBP722dB.bin
FloatDynamicRange\FloatDynamicRange -convert32 24 tmp\M843dBP722dB.bin  1048576 1 tmp\M843dBP843dB.bin

pause