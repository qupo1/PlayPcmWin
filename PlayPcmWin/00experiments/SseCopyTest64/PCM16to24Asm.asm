.data
align 16
;      f e  d c  b a  9 8  7 6  5 4  3 2  1 0
;  ↓ 0707 0606 0505 0404 0303 0202 0101 0000
;     oo04 04oo 0303 oo02 02oo 0101 oo00 00oo
mask0_0  DQ 0480030280010080H
mask0_0H DQ 8009088007068005H

;      f e  d c  b a  9 8  7 6  5 4  3 2  1 0
;  ↓ 0707 0606 0505 0404 0303 0202 0101 0000
;     oooo oooo oooo oooo 0707 oo06 06oo 0505
mask0_1  DQ 0f0e800d0c800b0aH
mask0_1H DQ 8080808080808080H

;      f e  d c  b a  9 8  7 6  5 4  3 2  1 0
;  ↓ 0f0f 0e0e 0d0d 0c0c 0b0b 0a0a 0909 0808
;     0aoo 0909 oo08 08oo oooo oooo oooo oooo
mask1_1  DQ 8080808080808080H
mask1_1H DQ 0480030280010080H

;      f e  d c  b a  9 8  7 6  5 4  3 2  1 0
;  ↓ 0f0f 0e0e 0d0d 0c0c 0b0b 0a0a 0909 0808
;     0f0f oo0e 0eoo 0d0d oo0c 0coo 0b0b oo0a
mask1_2  DQ 8009088007068005H
mask1_2H DQ 0f0e800d0c800b0aH

public PCM16to24Asm

.code

; save不要のレジスタ: RAX RCX RDX R8 R9 R10 R11 XMM0 to XMM5

; SSSE3使用。

; PCM16to24Asm(const char *src, int32_t *dst, int64_t count)
; src      --> rcx
; dst      --> rdx
; count    --> r8
align 16
PCM16to24Asm proc frame
    .endprolog

    ; srcBytesを算出し、srcバッファの終わりのアドレスを算出。
    mov r10, rcx  ; r10: src address
    mov r11, rdx  ; r11: dst address

    mov rax, r8   ; calc srcBytes, that is count * 2
    mov rcx, 2    ;
    mul rcx       ; rax: srcBytes , rdx:rax := rax * 2
    mov r9, rax   ; r9: srcBytes
    add r10, rax  ; now r10 points the end of src buffer

    mov rcx, r9   ; rcx: srcBytes
    neg rcx       ; now r10+rcx points the start of the src buffer
    mov rdx, r11  ; rdx: dst

align 16
LoopBegin:

    ; 1ループで16個処理します。
                              ;        f e  d c  b a  9 8  7 6  5 4  3 2  1 0
    movdqu xmm0, [r10+rcx]    ; xmm0: 0707 0606 0505 0404 0303 0202 0101 0000
    movdqu xmm1, [r10+rcx+16] ; xmm1: 0f0f 0e0e 0d0d 0c0c 0b0b 0a0a 0909 0808

    ; 1組目の4 PCM:                   oo04 04oo 0303 oo02 02oo 0101 oo00 00oo
    movdqa xmm2, xmmword ptr mask0_0
    movdqa xmm3, xmm0         ; xmm3: 0707 0606 0505 0404 0303 0202 0101 0000
    pshufb xmm3, xmm2         ; xmm3: oo04 04oo 0303 oo02 02oo 0101 oo00 00oo
    movntdq [rdx], xmm3        ; store 4 qword data.

    ; 2組目の4 PCM:                   0aoo 0909 oo08 08oo 0707 oo06 06oo 0505
    movdqa xmm2, xmmword ptr mask0_1
    pshufb xmm0, xmm2         ; xmm0: oooo oooo oooo oooo 0707 oo06 06oo 0505

    movdqa xmm2, xmmword ptr mask1_1
    movdqa xmm3, xmm1         ; xmm3: 0f0f 0e0e 0d0d 0c0c 0b0b 0a0a 0909 0808
    pshufb xmm3, xmm2         ; xmm3: 0aoo 0909 oo08 08oo oooo oooo oooo oooo
    paddb  xmm0, xmm3         ; xmm0 := xmm0 + xmm3
    movntdq [rdx+16], xmm0     ; store 4 qword data.

    ; 3組目の4 PCM:                   0f0f oo0e 0eoo 0d0d oo0c 0coo 0b0b oo0a
    movdqa xmm2, xmmword ptr mask1_2
    pshufb xmm1, xmm2         ; xmm1: 0f0f oo0e 0eoo 0d0d oo0c 0coo 0b0b oo0a
    movntdq [rdx+32], xmm1     ; store 4 qword data.

    add rdx, 48
    add rcx, 32

    jnz LoopBegin
    ret

align 16
PCM16to24Asm endp
end

