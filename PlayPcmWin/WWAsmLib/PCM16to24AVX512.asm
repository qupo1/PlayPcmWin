STACKBYTES    equ 16

.data
align 16

;      f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0
;  ↓ 0707 0606 0505 0404 - 0303 0202 0101 0000 : 0707 0606 0505 0404 - 0303 0202 0101 0000 : 0707 0606 0505 0404 - 0303 0202 0101 0000 : 0707 0606 0505 0404 - 0303 0202 0101 0000
;     oooo oooo oooo oooo - oooo oooo oooo oooo : oooo oooo oooo oooo - oooo oooo oooo oooo : oooo oooo oooo oooo - 0707 oo06 06oo 0505 : oo04 04oo 0303 oo02 - 02oo 0101 oo00 00oo
mask0_0  DQ 0480030280010080H
mask0_0b DQ 8009088007068005H
mask0_0c DQ 0f0e800d0c800b0aH
mask0_0d DQ 8080808080808080H
mask0_0e DQ 8080808080808080H
mask0_0f DQ 8080808080808080H
mask0_0g DQ 8080808080808080H
mask0_0h DQ 8080808080808080H


;      f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0
;  ↓ 0f0f 0e0e 0d0d 0c0c - 0b0b 0a0a 0909 0808 : 0f0f 0e0e 0d0d 0c0c - 0b0b 0a0a 0909 0808 : 0f0f 0e0e 0d0d 0c0c - 0b0b 0a0a 0909 0808 : 0f0f 0e0e 0d0d 0c0c - 0b0b 0a0a 0909 0808
;     oooo oooo oooo oooo - oooo oooo oooo oooo : 0f0f oo0e 0eoo 0d0d - oo0c 0coo 0b0b oo0a : 0aoo 0909 oo08 08oo - oooo oooo oooo oooo : oooo oooo oooo oooo - oooo oooo oooo oooo
mask1_0  DQ 8080808080808080H
mask1_0b DQ 8080808080808080H
mask1_0c DQ 8080808080808080H
mask1_0d DQ 0480030280010080H
mask1_0e DQ 8009088007068005H
mask1_0f DQ 0f0e800d0c800b0aH
mask1_0g DQ 8080808080808080H
mask1_0h DQ 8080808080808080H


;      f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0
;  ↓ 1717 1616 1515 1414 - 1313 1212 1111 1010 : 1717 1616 1515 1414 - 1313 1212 1111 1010 : 1717 1616 1515 1414 - 1313 1212 1111 1010 : 1717 1616 1515 1414 - 1313 1212 1111 1010
;     oo14 14oo 1313 oo12 - 12oo 1111 oo10 10oo : oooo oooo oooo oooo - oooo oooo oooo oooo : oooo oooo oooo oooo - oooo oooo oooo oooo : oooo oooo oooo oooo - oooo oooo oooo oooo
mask2_0  DQ 8080808080808080H
mask2_0b DQ 8080808080808080H
mask2_0c DQ 8080808080808080H
mask2_0d DQ 8080808080808080H
mask2_0e DQ 8080808080808080H
mask2_0f DQ 8080808080808080H
mask2_0g DQ 0480030280010080H
mask2_0h DQ 8009088007068005H


;      f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0
;  ↓ 1717 1616 1515 1414 - 1313 1212 1111 1010 : 1717 1616 1515 1414 - 1313 1212 1111 1010 : 1717 1616 1515 1414 - 1313 1212 1111 1010 : 1717 1616 1515 1414 - 1313 1212 1111 1010
;     oooo oooo oooo oooo - oooo oooo oooo oooo : oooo oooo oooo oooo - oooo oooo oooo oooo : oooo oooo oooo oooo - oooo oooo oooo oooo : oooo oooo oooo oooo - 1717 oo16 16oo 1515
mask2_1  DQ 0f0e800d0c800b0aH
mask2_1b DQ 8080808080808080H
mask2_1c DQ 8080808080808080H
mask2_1d DQ 8080808080808080H
mask2_1e DQ 8080808080808080H
mask2_1f DQ 8080808080808080H
mask2_1g DQ 8080808080808080H
mask2_1h DQ 8080808080808080H

;      f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0
;  ↓ 1f1f 1e1e 1d1d 1c1c - 1b1b 1a1a 1919 1818 : 1f1f 1e1e 1d1d 1c1c - 1b1b 1a1a 1919 1818 : 1f1f 1e1e 1d1d 1c1c - 1b1b 1a1a 1919 1818 : 1f1f 1e1e 1d1d 1c1c - 1b1b 1a1a 1919 1818
;     oooo oooo oooo oooo - oooo oooo oooo oooo : oooo oooo oooo oooo - oooo oooo oooo oooo : 1f1f oo1e 1eoo 1d1d - oo1c 1coo 1b1b oo1a : 1aoo 1919 oo18 18oo - oooo oooo oooo oooo
mask3_1  DQ 8080808080808080H
mask3_1b DQ 0480030280010080H
mask3_1c DQ 8009088007068005H
mask3_1d DQ 0f0e800d0c800b0aH
mask3_1e DQ 8080808080808080H
mask3_1f DQ 8080808080808080H
mask3_1g DQ 8080808080808080H
mask3_1h DQ 8080808080808080H

;      f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0
;  ↓ 2727 2626 2525 2424 - 2323 2222 2121 2020 : 2727 2626 2525 2424 - 2323 2222 2121 2020 : 2727 2626 2525 2424 - 2323 2222 2121 2020 : 2727 2626 2525 2424 - 2323 2222 2121 2020
;     oooo oooo oooo oooo - 2727 oo26 26oo 2525 : oo24 24oo 2323 oo22 - 22oo 2121 oo20 20oo : oooo oooo oooo oooo - oooo oooo oooo oooo : oooo oooo oooo oooo - oooo oooo oooo oooo
mask4_1  DQ 8080808080808080H
mask4_1b DQ 8080808080808080H
mask4_1c DQ 8080808080808080H
mask4_1d DQ 8080808080808080H
mask4_1e DQ 0480030280010080H
mask4_1f DQ 8009088007068005H
mask4_1g DQ 0f0e800d0c800b0aH
mask4_1h DQ 8080808080808080H

;      f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0
;  ↓ 2f2f 2e2e 2d2d 2c2c - 2b2b 2a2a 2929 2828 : 2f2f 2e2e 2d2d 2c2c - 2b2b 2a2a 2929 2828 : 2f2f 2e2e 2d2d 2c2c - 2b2b 2a2a 2929 2828 : 2f2f 2e2e 2d2d 2c2c - 2b2b 2a2a 2929 2828
;     2aoo 2929 oo28 28oo - oooo oooo oooo oooo : oooo oooo oooo oooo - oooo oooo oooo oooo : oooo oooo oooo oooo - oooo oooo oooo oooo : oooo oooo oooo oooo - oooo oooo oooo oooo
mask5_1  DQ 8080808080808080H
mask5_1b DQ 8080808080808080H
mask5_1c DQ 8080808080808080H
mask5_1d DQ 8080808080808080H
mask5_1e DQ 8080808080808080H
mask5_1f DQ 8080808080808080H
mask5_1g DQ 8080808080808080H
mask5_1h DQ 0480030280010080H

;      f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0
;  ↓ 2f2f 2e2e 2d2d 2c2c - 2b2b 2a2a 2929 2828 : 2f2f 2e2e 2d2d 2c2c - 2b2b 2a2a 2929 2828 : 2f2f 2e2e 2d2d 2c2c - 2b2b 2a2a 2929 2828 : 2f2f 2e2e 2d2d 2c2c - 2b2b 2a2a 2929 2828
;     oooo oooo oooo oooo - oooo oooo oooo oooo : oooo oooo oooo oooo - oooo oooo oooo oooo : oooo oooo oooo oooo - oooo oooo oooo oooo : 2f2f oo2e 2eoo 2d2d - oo2c 2coo 2b2b oo2a 
mask5_2  DQ 8009088007068005H
mask5_2b DQ 0f0e800d0c800b0aH
mask5_2c DQ 8080808080808080H
mask5_2d DQ 8080808080808080H
mask5_2e DQ 8080808080808080H
mask5_2f DQ 8080808080808080H
mask5_2g DQ 8080808080808080H
mask5_2h DQ 8080808080808080H

;      f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0
;  ↓ 3737 3636 3535 3434 - 3333 3232 3131 3030 : 3737 3636 3535 3434 - 3333 3232 3131 3030 : 3737 3636 3535 3434 - 3333 3232 3131 3030 : 3737 3636 3535 3434 - 3333 3232 3131 3030
;                                                                       3737 oo36 36oo 3535 : oo34 34oo 3333 oo32 - 32oo 3131 oo30 30oo : oooo oooo oooo oooo - oooo oooo oooo oooo
mask6_2  DQ 8080808080808080H
mask6_2b DQ 8080808080808080H
mask6_2c DQ 0480030280010080H
mask6_2d DQ 8009088007068005H
mask6_2e DQ 0f0e800d0c800b0aH
mask6_2f DQ 8080808080808080H
mask6_2g DQ 8080808080808080H
mask6_2h DQ 8080808080808080H

;      f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0
;  ↓ 3f3f 3e3e 3d3d 3c3c - 3b3b 3a3a 3939 3838 : 3f3f 3e3e 3d3d 3c3c - 3b3b 3a3a 3939 3838 : 3f3f 3e3e 3d3d 3c3c - 3b3b 3a3a 3939 3838 : 3f3f 3e3e 3d3d 3c3c - 3b3b 3a3a 3939 3838
;     3f3f oo3e 3eoo 3d3d - oo3c 3coo 3b3b oo3a : 3aoo 3939 oo38 38oo
mask7_2  DQ 8080808080808080H
mask7_2b DQ 8080808080808080H
mask7_2c DQ 8080808080808080H
mask7_2d DQ 8080808080808080H
mask7_2e DQ 8080808080808080H
mask7_2f DQ 0480030280010080H
mask7_2g DQ 8009088007068005H
mask7_2h DQ 0f0e800d0c800b0aH


public PCM16to24AVX512

.code

SaveRegisters MACRO
    sub rsp,STACKBYTES
   .allocstack STACKBYTES
    mov [rsp],rsi
   .savereg rsi,0
    mov [rsp+8],rdi
   .savereg rdi,8
   .endprolog
ENDM
 
RestoreRegisters MACRO
    mov rsi, [rsp]
    mov rdi, [rsp+8]
    add rsp,STACKBYTES
ENDM

; save不要のレジスタ: RAX RCX RDX R8 R9 R10 R11 XMM0 to XMM5

; AVX512F, AVX512BW

; PCM16to24AVX512(const char *src, int32_t *dst, int64_t count)
; src      --> rcx
; dst      --> rdx
; count    --> r8
align 16
PCM16to24AVX512 proc frame
    SaveRegisters

    ; dstBytesを算出し、srcバッファ、dstバッファの終わりのアドレスを算出。

    mov rsi, rcx  ; rsi: src address
    mov rdi, rdx  ; rdi: dst address

    mov rax, r8   ; calc srcBytes, that is count*2
    mov rcx, 2    ; rcx := 2
    mul rcx       ; rax: srcBytes , rdx:rax := rax * 2
    mov r9, rax   ; r9: srcBytes
    add rsi, rax  ; now rsi points the end of src buffer

    xor rdx, rdx  ; rdx := 0

    mov rcx, r9   ; rcx: srcBytes
    neg rcx       ; now rsi+rcx points the start of the src buffer
                  ; and rdi+rdx points the start of the dst buffer

align 16
LoopBegin:

    ; 1ループで64個処理します。
                                                    ;       3f3e 3d3c 3b3a 3938 - 3736 3534 3332 3130 : 2f2e 2d2c 2b2a 2928 - 2726 2524 2322 2120 : 1f1e 1d1c 1b1a 1918 - 1716 1514 1312 1110 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0
    vbroadcasti32x4 zmm0, xmmword ptr [rsi+rcx]     ; zmm0: 0707 0606 0505 0404 - 0303 0202 0101 0000 : 0707 0606 0505 0404 - 0303 0202 0101 0000 : 0707 0606 0505 0404 - 0303 0202 0101 0000 : 0707 0606 0505 0404 - 0303 0202 0101 0000

    vmovdqu64       zmm2, zmmword ptr mask0_0
    vpshufb         zmm2, zmm0, zmm2

    vbroadcasti32x4 zmm0, xmmword ptr [rsi+rcx+16]  ; zmm0: 0f0f 0e0e 0d0d 0c0c - 0b0b 0a0a 0909 0808 : ...

    vmovdqu64       zmm3, zmmword ptr mask1_0
    vpshufb         zmm3, zmm0, zmm3

    vporq          zmm3, zmm3, zmm2

    vbroadcasti32x4 zmm0, xmmword ptr [rsi+rcx+32]  ; zmm0: 1717 1616 1515 1414 - 1313 1212 1111 1010 : ...

    vmovdqu64      zmm2, zmmword ptr mask2_0
    vpshufb        zmm2, zmm0, zmm2
    vporq          zmm3, zmm3, zmm2

    vmovntdq zmmword ptr [rdi + rdx], zmm3  ; 1個目出力。

                                                    ; zmm0: 1717 1616 1515 1414 - 1313 1212 1111 1010 : ...
    vmovdqu64      zmm2, zmmword ptr mask2_1
    vpshufb        zmm2, zmm0, zmm2

    vbroadcasti32x4 zmm0, xmmword ptr [rsi+rcx+48]  ; zmm0: 1f1f 1e1e 1d1d 1c1c - 1b1b 1a1a 1919 1818 : ...
    vmovdqu64      zmm3, zmmword ptr mask3_1
    vpshufb        zmm3, zmm0, zmm3
    vporq          zmm3, zmm3, zmm2

    vbroadcasti32x4 zmm0, xmmword ptr [rsi+rcx+64]  ; zmm0: 2727 2626 2525 2424 - 2323 2222 2121 2020 : ...
    vmovdqu64      zmm2, zmmword ptr mask4_1
    vpshufb        zmm2, zmm0, zmm2
    vporq          zmm3, zmm3, zmm2

    vbroadcasti32x4 zmm0, xmmword ptr [rsi+rcx+80]  ; zmm0: 2f2f 2e2e 2d2d 2c2c - 2b2b 2a2a 2929 2828 : ...
    vmovdqu64      zmm2, zmmword ptr mask5_1
    vpshufb        zmm2, zmm0, zmm2
    vporq          zmm3, zmm3, zmm2

    vmovntdq zmmword ptr [rdi + rdx +64], zmm3  ; 2個目出力。

                                                    ; zmm0: 2f2f 2e2e 2d2d 2c2c - 2b2b 2a2a 2929 2828 : ...
    vmovdqu64      zmm2, zmmword ptr mask5_2
    vpshufb        zmm2, zmm0, zmm2

    vbroadcasti32x4 zmm0, xmmword ptr [rsi+rcx+96]  ; zmm0: 3737 3636 3535 3434 - 3333 3232 3131 3030 : ...
    vmovdqu64      zmm3, zmmword ptr mask6_2
    vpshufb        zmm3, zmm0, zmm3
    vporq          zmm3, zmm3, zmm2

    vbroadcasti32x4 zmm0, xmmword ptr [rsi+rcx+112] ; zmm0: 3f3f 3e3e 3d3d 3c3c - 3b3b 3a3a 3939 3838 : ...
    vmovdqu64      zmm2, zmmword ptr mask7_2
    vpshufb        zmm2, zmm0, zmm2
    vporq          zmm3, zmm3, zmm2

    vmovntdq zmmword ptr [rdi + rdx +128], zmm3  ; 3個目出力。

    add rdx, 192
    add rcx, 128

    jnz LoopBegin

    RestoreRegisters
    vzeroupper
    ret

align 16
PCM16to24AVX512 endp
end

