.data
align 16
;      f e  d c  b a  9 8  7 6  5 4  3 2  1 0
;  ↓ 5544 4444 3333 3322 2222 1111 1100 0000
;     3333 33oo 2222 22oo 1111 11oo 0000 00oo
mask0_0  DQ 0504038002010080H
mask0_0H DQ 0b0a098008070680H

;      f e  d c  b a  9 8  7 6  5 4  3 2  1 0
;  ↓ 5544 4444 3333 3322 2222 1111 1100 0000
;     oooo oooo oooo oooo oooo 55oo 4444 44oo
mask0_1  DQ 80800f800e0d0c80H
mask0_1H DQ 8080808080808080H

;      f e  d c  b a  9 8  7 6  5 4  3 2  1 0
;  ↓ aaaa 9999 9988 8888 7777 7766 6666 5555
;     7777 77oo 6666 6600 5555 oooo oooo oooo
mask1_1  DQ 0100808080808080H
mask1_1H DQ 0706058004030280H

;      f e  d c  b a  9 8  7 6  5 4  3 2  1 0
;  ↓ aaaa 9999 9988 8888 7777 7766 6666 5555
;     oooo oooo ooaa aa00 9999 9900 8888 8800
mask1_2  DQ 0d0c0b800a090880H
mask1_2H DQ 80808080800f0e80H

;      f e  d c  b a  9 8  7 6  5 4  3 2  1 0
;  ↓ ffff ffee eeee dddd ddcc cccc bbbb bbaa
;     bbbb bboo aaoo oooo oooo oooo oooo oooo
mask2_2  DQ 8080808080808080H
mask2_2H DQ 0302018000808080H

;      f e  d c  b a  9 8  7 6  5 4  3 2  1 0
;  ↓ ffff ffee eeee dddd ddcc cccc bbbb bbaa
;     ffff ff00 eeee ee00 dddd dd00 cccc cc00
mask2_3  DQ 0908078006050480H
mask2_3H DQ 0f0e0d800c0b0a80H

public PCM24to32Asm

.code

; save不要のレジスタ: RAX RCX RDX R8 R9 R10 R11 XMM0 XMM1 XMM2 XMM3 XMM4 XMM5

; PCM24to32Asm(const short *src, int *dst, int64_t count)
; src      --> rcx
; dst      --> rdx
; count    --> r8
align 8
PCM24to32Asm proc frame
    .endprolog
    ;
    ; dstBytesを算出し、dstバッファ、srcバッファの終わりのアドレスを算出。
    mov r10, rcx  ; r10: src address
    mov r11, rdx  ; r11: dst address
    ;
    mov rax, r8   ; calc srcBytes, that is count*3
    mov rcx, 3    ;
    mul rcx       ; rax: srcBytes , rdx:rax := rax * 3
    mov r9, rax   ; r9: srcBytes
    add r10, rax  ; now r10 points the end of src buffer
    ;
    mov rcx, r9   ; rcx: srcBytes
    neg rcx       ; now r10+rcx points the start of the src buffer
    mov rdx, r11  ; rdx: dst

align 8
LoopBegin:

    ; 16個ずつ処理します。
                              ;        f e  d c  b a  9 8  7 6  5 4  3 2  1 0
    movdqa xmm0, [r10+rcx]    ; xmm0: 5544 4444 3333 3322 2222 1111 1100 0000
    movdqa xmm1, [r10+rcx+16] ; xmm1: aaaa 9999 9988 8888 7777 7766 6666 5555
    movdqa xmm2, [r10+rcx+32] ; xmm2: ffff ffee eeee dddd ddcc cccc bbbb bbaa

    ; 1組目の4 PCM:                   3333 33oo 2222 22oo 1111 11oo 0000 00oo
    movdqa xmm3, xmmword ptr mask0_0
    movdqa xmm4, xmm0         ; xmm4: 5544 4444 3333 3322 2222 1111 1100 0000
    pshufb xmm4, xmm3         ; xmm4: 3333 33oo 2222 22oo 1111 11oo 0000 00oo
    movdqa [rdx], xmm4        ; store 4 qword data.

    ; 2組目の4 PCM:                   7777 77oo 6666 66oo 5555 55oo 4444 44oo
    movdqa xmm3, xmmword ptr mask0_1
    pshufb xmm0, xmm3         ; xmm0: oooo oooo oooo oooo oooo 55oo 4444 44oo
    ;
    movdqa xmm3, xmmword ptr mask1_1
    movdqa xmm4, xmm1         ; xmm4: aaaa 9999 9988 8888 7777 7766 6666 5555
    pshufb xmm4, xmm3         ; xmm4: 7777 77oo 6666 6600 5555 oooo oooo oooo
    paddb  xmm0, xmm4         ; xmm0 := xmm0 + xmm4
    movdqa [rdx+16], xmm0     ; store 4 qword data.

    ; 3組目の4 PCM:                   bbbb bboo aaaa aaoo 9999 99oo 8888 88oo
    movdqa xmm3, xmmword ptr mask1_2
    pshufb xmm1, xmm3         ; xmm1: oooo oooo ooaa aaoo 9999 99oo 8888 88oo
    ;
    movdqa xmm3, xmmword ptr mask2_2
    movdqa xmm4, xmm2         ; xmm4: ffff ffee eeee dddd ddcc cccc bbbb bbaa
    pshufb xmm4, xmm3         ; xmm4: bbbb bboo aaoo oooo oooo oooo oooo oooo
    paddb  xmm1, xmm4         ; xmm1 := xmm1 + xmm4
    movdqa [rdx+32], xmm1     ; store 4 qword data.

    ; 4組目の4 PCM:                   ffff ffoo eeee eeoo dddd ddoo cccc ccoo
    movdqa xmm3, xmmword ptr mask2_3
    pshufb xmm2, xmm3         ; xmm2: ffff ffoo eeee eeoo dddd ddoo cccc ccoo
    movdqa [rdx+48], xmm2     ; store 4 qword data.

    add rdx, 64
    add rcx, 48

    jnz LoopBegin
    ret

align 8
PCM24to32Asm endp
end

