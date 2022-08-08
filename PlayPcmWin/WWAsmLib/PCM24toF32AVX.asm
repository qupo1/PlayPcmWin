STACKBYTES    equ 16

.data
align 16

;      f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0
;  ↓ 0504 0404 0303 0302 - 0202 0101 0100 0000 : 0504 0404 0303 0302 - 0202 0101 0100 0000
;     oooo oooo oooo oooo - oooo 05oo 0404 04oo : 0303 03oo 0202 02oo - 0101 01oo 0000 00oo
mask0_0  DQ 0504038002010080H
mask0_0b DQ 0b0a098008070680H
mask0_0c DQ 80800f800e0d0c80H
mask0_0d DQ 8080808080808080H


;      f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8  - 7 6  5 4  3 2  1 0
;  ↓ 0a0a 0909 0908 0808 - 0707 0706 0606 0505 : 0a0a 0909 0908 0808 - 0707 0706 0606 0505
;     0707 07oo 0606 06oo - 0505 oooo oooo oooo : oooo oooo oooo oooo - oooo oooo oooo oooo
mask1_0  DQ 8080808080808080H
mask1_0b DQ 8080808080808080H
mask1_0c DQ 0100808080808080H
mask1_0d DQ 0706058004030280H

;      f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8  - 7 6  5 4  3 2  1 0
;  ↓ 0a0a 0909 0908 0808 - 0707 0706 0606 0505 : 0a0a 0909 0908 0808 - 0707 0706 0606 0505
;     oooo oooo oooo oooo - oooo oooo oooo oooo : oooo oooo oo0a 0aoo - 0909 09oo 0808 08oo
mask1_1  DQ 0d0c0b800a090880H
mask1_1b DQ 80808080800f0e80H
mask1_1c DQ 8080808080808080H
mask1_1d DQ 8080808080808080H

;      f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0 :  f e  d c  b a  9 8  - 7 6  5 4  3 2  1 0
;  ↓ 0f0f 0f0e 0e0e 0d0d - 0d0c 0c0c 0b0b 0b0a : 0f0f 0f0e 0e0e 0d0d - 0d0c 0c0c 0b0b 0b0a
;     0f0f 0foo 0e0e 0eoo - 0d0d 0doo 0c0c 0coo : 0b0b 0boo 0aoo oooo - oooo oooo oooo oooo
mask2_1  DQ 8080808080808080H
mask2_1b DQ 0302018000808080H
mask2_1c DQ 0908078006050480H
mask2_1d DQ 0f0e0d800c0b0a80H

; 定数1.0f/(32768.0f* 65536.0f)が8個。
PCM32toPCM32F  DQ 3000000030000000H
PCM32toPCM32Fb DQ 3000000030000000H
PCM32toPCM32Fc DQ 3000000030000000H
PCM32toPCM32Fd DQ 3000000030000000H


public PCM24toF32AVX

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

; PCM24toF32AVX(const char *src, int32_t *dst, int64_t count)
; src      --> rcx
; dst      --> rdx
; count    --> r8
align 16
PCM24toF32AVX proc frame
    SaveRegisters

    ; dstBytesを算出し、srcバッファ、dstバッファの終わりのアドレスを算出。

    mov rsi, rcx  ; rsi: src address
    mov rdi, rdx  ; rdi: dst address

    mov rax, r8   ; calc srcBytes, that is count*3
    mov rcx, 3    ; rcx := 3
    mul rcx       ; rax: srcBytes , rdx:rax := rax * 3
    mov r9, rax   ; r9: srcBytes
    add rsi, rax  ; now rsi points the end of src buffer

    xor rdx, rdx  ; rdx := 0

    mov rcx, r9   ; rcx: srcBytes
    neg rcx       ; now rsi+rcx points the start of the src buffer
                  ; and rdi+rdx points the start of the dst buffer

align 16
LoopBegin:

    ; 1ループで16個処理します。

                                                  ;       1f1e 1d1c 1b1a 1918 - 1716 1514 1312 1110 :  f e  d c  b a  9 8 -  7 6  5 4  3 2  1 0
    vbroadcasti128 ymm0, xmmword ptr [rsi+rcx]    ; ymm0: 0504 0404 0303 0302 - 0202 0101 0100 0000 : 0504 0404 0303 0302 - 0202 0101 0100 0000
    vbroadcasti128 ymm1, xmmword ptr [rsi+rcx+16] ; ymm1: 0a0a 0909 0908 0808 - 0707 0706 0606 0505 : 0a0a 0909 0908 0808 - 0707 0706 0606 0505

    vmovdqu        ymm2, ymmword ptr mask0_0
    vpshufb        ymm2, ymm0, ymm2

    vmovdqu        ymm3, ymmword ptr mask1_0
    vpshufb        ymm3, ymm1, ymm3

    vpor           ymm3, ymm3, ymm2

    ; 符号付きdword→float変換。
    vcvtdq2ps  ymm3, ymm3                        ; convert signed dword to float
    vmovdqu    ymm2, ymmword ptr PCM32toPCM32F   ; ymm2 : 1.0f / (32768.0f * 65536.0f)を4個置く。
    vmulps     ymm3, ymm3, ymm2                  ; ymm3 := ymm3 * ymm2, scale float value to [-1 1)

    vmovntdq ymmword ptr [rdi + rdx], ymm3  ; 1個目出力。

    vbroadcasti128 ymm0, xmmword ptr [rsi+rcx+32]  ; ymm0: 0f0f 0f0e 0e0e 0d0d - 0d0c 0c0c 0b0b 0b0a : 0f0f 0f0e 0e0e 0d0d - 0d0c 0c0c 0b0b 0b0a
                                                   ; ymm1: 0a0a 0909 0908 0808 - 0707 0706 0606 0505 : 0a0a 0909 0908 0808 - 0707 0706 0606 0505

    vmovdqu        ymm2, ymmword ptr mask1_1
    vpshufb        ymm2, ymm1, ymm2

    vmovdqu        ymm3, ymmword ptr mask2_1
    vpshufb        ymm3, ymm0, ymm3

    vpor           ymm3, ymm3, ymm2

    ; 符号付きdword→float変換。
    vcvtdq2ps  ymm3, ymm3                        ; convert signed dword to float
    vmovdqu    ymm2, ymmword ptr PCM32toPCM32F   ; ymm2 : 1.0f / (32768.0f * 65536.0f)を4個置く。
    vmulps     ymm3, ymm3, ymm2                  ; ymm3 := ymm3 * ymm2, scale float value to [-1 1)

    vmovntdq ymmword ptr [rdi + rdx +32], ymm3  ; 2個目出力。

    add rdx, 64
    add rcx, 48

    jnz LoopBegin

    RestoreRegisters
    vzeroupper
    ret

align 16
PCM24toF32AVX endp
end
