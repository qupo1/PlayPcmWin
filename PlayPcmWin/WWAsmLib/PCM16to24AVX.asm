STACKBYTES    equ 16

.data
align 16

;      f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0
;  ↓ 0707 0606 0505 0404 - 0303 0202 0101 0000 : 0707 0606 0505 0404 - 0303 0202 0101 0000
;     oooo oooo oooo oooo - 0707 oo06 06oo 0505 : oo04 04oo 0303 oo02 - 02oo 0101 oo00 00oo
mask0_0  DQ 0480030280010080H
mask0_0b DQ 8009088007068005H
mask0_0c DQ 0f0e800d0c800b0aH
mask0_0d DQ 8080808080808080H


;      f e  d c  b a  9 8 - 7 6  5 4  3 2  1 0  : f e  d c  b a  9 8  - 7 6  5 4  3 2  1 0
;  ↓ 0f0f 0e0e 0d0d 0c0c - 0b0b 0a0a 0909 0808 : 0f0f 0e0e 0d0d 0c0c - 0b0b 0a0a 0909 0808
;     0aoo 0909 oo08 08oo - oooo oooo oooo oooo : oooo oooo oooo oooo - oooo oooo oooo oooo
mask1_0  DQ 8080808080808080H
mask1_0b DQ 8080808080808080H
mask1_0c DQ 8080808080808080H
mask1_0d DQ 0480030280010080H

;      f e  d c  b a  9 8 - 7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8  - 7 6  5 4  3 2  1 0
;  ↓ 0f0f 0e0e 0d0d 0c0c - 0b0b 0a0a 0909 0808 : 0f0f 0e0e 0d0d 0c0c - 0b0b 0a0a 0909 0808
;     oooo oooo oooo oooo - oooo oooo oooo oooo : 0f0f oo0e 0eoo 0d0d - oo0c 0coo 0b0b oo0a
mask1_1  DQ 8009088007068005H
mask1_1b DQ 0f0e800d0c800b0aH
mask1_1c DQ 8080808080808080H
mask1_1d DQ 8080808080808080H

;      f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0
;  ↓ 1717 1616 1515 1414 - 1313 1212 1111 1010 : 1717 1616 1515 1414 - 1313 1212 1111 1010
;     oo14 14oo 1313 oo12 - 12oo 1111 oo10 10oo : oooo oooo oooo oooo - oooo oooo oooo oooo
mask2_1  DQ 8080808080808080H
mask2_1b DQ 8080808080808080H
mask2_1c DQ 0480030280010080H
mask2_1d DQ 8009088007068005H

;      f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0
;  ↓ 1717 1616 1515 1414 - 1313 1212 1111 1010 : 1717 1616 1515 1414 - 1313 1212 1111 1010
;     oooo oooo oooo oooo - oooo oooo oooo oooo : oooo oooo oooo oooo - 1717 oo16 16oo 1515
mask2_2  DQ 0f0e800d0c800b0aH
mask2_2b DQ 8080808080808080H
mask2_2c DQ 8080808080808080H
mask2_2d DQ 8080808080808080H

;      f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0
;  ↓ 1f1f 1e1e 1d1d 1c1c - 1b1b 1a1a 1919 1818 : 1f1f 1e1e 1d1d 1c1c - 1b1b 1a1a 1919 1818
;     1f1f oo1e 1eoo 1d1d   oo1c 1coo 1b1b oo1a : 1aoo 1919 oo18 18oo - oooo oooo oooo oooo
mask3_2  DQ 8080808080808080H
mask3_2b DQ 0480030280010080H
mask3_2c DQ 8009088007068005H
mask3_2d DQ 0f0e800d0c800b0aH


public PCM16to24AVX

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

; AVX2

; PCM16to24AVX(const char *src, int32_t *dst, int64_t count)
; src      --> rcx
; dst      --> rdx
; count    --> r8
align 16
PCM16to24AVX proc frame
    SaveRegisters

    ; dstBytesを算出し、srcバッファ、dstバッファの終わりのアドレスを算出。

    mov rsi, rcx  ; rsi: src address
    mov rdi, rdx  ; rdi: dst address

    mov rax, r8   ; calc srcBytes, that is count*2
    mov rcx, 2    ; rcx := 2
    mul rcx       ; rax: srcBytes , rdx:rax := rax * 2
    mov r9, rax   ; r9: srcBytes
    add rsi, rax  ; now rsi points the end of src buffer

    xor rdx, rdx  ; rdi := 0

    mov rcx, r9   ; rcx: srcBytes
    neg rcx       ; now rsi+rcx points the start of the src buffer
                  ; and rdi+rdx points the start of the dst buffer

align 16
LoopBegin:

    ; 1ループで32個処理します。
                                                  ;       1f1e 1d1c 1b1a 1918 - 1716 1514 1312 1110 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0
    vbroadcasti128 ymm0, xmmword ptr [rsi+rcx]    ; ymm0: 0707 0606 0505 0404 - 0303 0202 0101 0000 : 0707 0606 0505 0404 - 0303 0202 0101 0000
    vbroadcasti128 ymm1, xmmword ptr [rsi+rcx+16] ; ymm1: 0f0f 0e0e 0d0d 0c0c - 0b0b 0a0a 0909 0808 : 0f0f 0e0e 0d0d 0c0c - 0b0b 0a0a 0909 0808

    vmovdqu        ymm2, ymmword ptr mask0_0
    vpshufb        ymm2, ymm0, ymm2

    vmovdqu        ymm3, ymmword ptr mask1_0
    vpshufb        ymm3, ymm1, ymm3

    vpor           ymm3, ymm3, ymm2

    vmovntdq ymmword ptr [rdi + rdx], ymm3  ; 1個目出力。

    vbroadcasti128 ymm0, xmmword ptr [rsi+rcx+32]  ; ymm0: 1717 1616 1515 1414 - 1313 1212 1111 1010 : 1717 1616 1515 1414 - 1313 1212 1111 1010
                                                   ; ymm1: 0f0f 0e0e 0d0d 0c0c - 0b0b 0a0a 0909 0808 : 0f0f 0e0e 0d0d 0c0c - 0b0b 0a0a 0909 0808

    vmovdqu        ymm2, ymmword ptr mask1_1
    vpshufb        ymm2, ymm1, ymm2

    vmovdqu        ymm3, ymmword ptr mask2_1
    vpshufb        ymm3, ymm0, ymm3

    vpor           ymm3, ymm3, ymm2

    vmovntdq ymmword ptr [rdi + rdx +32], ymm3  ; 2個目出力。

                                                   ; ymm0: 1717 1616 1515 1414 - 1313 1212 1111 1010 : 1717 1616 1515 1414 - 1313 1212 1111 1010
    vbroadcasti128 ymm1, xmmword ptr [rsi+rcx+48]  ; ymm1: 1f1f 1e1e 1d1d 1c1c - 1b1b 1a1a 1919 1818 : 1f1f 1e1e 1d1d 1c1c - 1b1b 1a1a 1919 1818

    vmovdqu        ymm2, ymmword ptr mask2_2
    vpshufb        ymm2, ymm0, ymm2

    vmovdqu        ymm3, ymmword ptr mask3_2
    vpshufb        ymm3, ymm1, ymm3

    vpor           ymm3, ymm3, ymm2

    vmovntdq ymmword ptr [rdi + rdx +64], ymm3  ; 3個目出力。

    add rdx, 96
    add rcx, 64

    jnz LoopBegin

    RestoreRegisters
    vzeroupper
    ret

align 16
PCM16to24AVX endp
end

