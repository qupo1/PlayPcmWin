STACKBYTES    equ 16*6

.data
align 16
;      f e  d c  b a  9 8  7 6  5 4  3 2  1 0
;  ↓ 5544 4444 3333 3322 2222 1111 1100 0000
;     3333 33oo 2222 22oo 1111 11oo 0000 00oo
mask00L DQ 0504038002010080H
mask00H DQ 0b0a098008070680H

;      f e  d c  b a  9 8  7 6  5 4  3 2  1 0
;  ↓ 5544 4444 3333 3322 2222 1111 1100 0000
;     oooo oooo oooo oooo oooo 55oo 4444 44oo
mask01L DQ 80800f800e0d0c80H
mask01H DQ 8080808080808080H

;      f e  d c  b a  9 8  7 6  5 4  3 2  1 0
;  ↓ aaaa 9999 9988 8888 7777 7766 6666 5555
;     7777 77oo 6666 6600 5555 oooo oooo oooo
mask11L DQ 0100808080808080H
mask11H DQ 0706058004030280H

;      f e  d c  b a  9 8  7 6  5 4  3 2  1 0
;  ↓ aaaa 9999 9988 8888 7777 7766 6666 5555
;     oooo oooo ooaa aa00 9999 9900 8888 8800
mask12L DQ 0d0c0b800a090880H
mask12H DQ 80808080800f0e80H

;      f e  d c  b a  9 8  7 6  5 4  3 2  1 0
;  ↓ ffff ffee eeee dddd ddcc cccc bbbb bbaa
;     bbbb bboo aaoo oooo oooo oooo oooo oooo
mask22L DQ 8080808080808080H
mask22H DQ 0302018000808080H

;      f e  d c  b a  9 8  7 6  5 4  3 2  1 0
;  ↓ ffff ffee eeee dddd ddcc cccc bbbb bbaa
;     ffff ff00 eeee ee00 dddd dd00 cccc cc00
mask23L DQ 0908078006050480H
mask23H DQ 0f0e0d800c0b0a80H

public PCM24to32Asm

.code

SaveRegisters MACRO
    sub rsp,STACKBYTES
   .allocstack STACKBYTES
   .endprolog
ENDM

RestoreRegisters MACRO
    add rsp,STACKBYTES
ENDM

; save不要のレジスタ: RAX RCX RDX R8 R9 R10 R11 XMM0 XMM1 XMM2 XMM3 XMM4 XMM5

; PCM24to32Asm(const short *src, int *dst, int64_t count)
; src      --> rcx
; dst      --> rdx
; count    --> r8
align 8
PCM24to32Asm proc frame
    SaveRegisters
    ; スタックに定数を置きます。
    mov rax, mask00L
    mov [rsp], rax
    mov rax, mask00H
    mov [rsp+8], rax
    mov rax, mask01L
    mov [rsp+8*2], rax
    mov rax, mask01H
    mov [rsp+8*3], rax
    mov rax, mask11L
    mov [rsp+8*4], rax
    mov rax, mask11H
    mov [rsp+8*5], rax
    mov rax, mask12L
    mov [rsp+8*6], rax
    mov rax, mask12H
    mov [rsp+8*7], rax
    mov rax, mask22L
    mov [rsp+8*8], rax
    mov rax, mask22H
    mov [rsp+8*9], rax
    mov rax, mask23L
    mov [rsp+8*10], rax
    mov rax, mask23H
    mov [rsp+8*11], rax
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
    mov rax, r8   ; calc dstBytes, that is count*4
    mov rcx, 4    ;
    mul rcx       ; rax: dstBytes , rdx:rax := rax * 4
    mov rdx, rax  ; rdx: dstBytes
    add r11, rax  ; now r11 points the end of dst buffer
    ;
    mov rcx, r9   ; rcx: srcBytes
    neg rcx       ; now r10+rcx points the start of the src buffer
    neg rdx       ; now r11+rdx points the start of the dst buffer
align 8
LabelBegin:
                             ; load 16 samples of 24bit PCM
                             ;        f e  d c  b a  9 8  7 6  5 4  3 2  1 0
    movdqa xmm0, [r10+rcx]   ; xmm0: 5544 4444 3333 3322 2222 1111 1100 0000
    add rcx, 16
    movdqa xmm1, [r10+rcx]   ; xmm1: aaaa 9999 9988 8888 7777 7766 6666 5555
    add rcx, 16

    ; 1組目の4 PCM:                  3333 33oo 2222 22oo 1111 11oo 0000 00oo
    movdqu xmm3, [rsp+0]
    movdqa xmm2, xmm0        ; xmm2: 5544 4444 3333 3322 2222 1111 1100 0000
    pshufb xmm2, xmm3        ; xmm2: 3333 33oo 2222 22oo 1111 11oo 0000 00oo
    movdqa [r11+rdx], xmm2   ; store 4 qword data.
    add rdx, 16

    ; 2組目の4 PCM:                  7777 77oo 6666 66oo 5555 55oo 4444 44oo
    movdqu xmm3, [rsp+16]
    movdqa xmm2, xmm0        ; xmm2: 5544 4444 3333 3322 2222 1111 1100 0000
    pshufb xmm2, xmm3        ; xmm2: oooo oooo oooo oooo oooo 5500 4444 4400
    ;
    movdqu xmm3, [rsp+16*2]
    movdqa xmm4, xmm1        ; xmm4: aaaa 9999 9988 8888 7777 7766 6666 5555
    pshufb xmm4, xmm3        ; xmm4: 7777 77oo 6666 6600 5555 oooo oooo oooo
    paddb  xmm2, xmm4        ; xmm2 := xmm2 + xmm4
    movdqa [r11+rdx], xmm2   ; store 4 qword data.
    add rdx, 16

    movdqa xmm0, [r10+rcx]   ; xmm0: ffff ffee eeee dddd ddcc cccc bbbb bbaa
    ; 最後のrcx addはjnzの直前に実行します。読みやすい感じがするので。

    ; 3組目の4 PCM:                  bbbb bboo aaaa aaoo 9999 99oo 8888 88oo
    movdqu xmm3, [rsp+16*3]
    movdqa xmm2, xmm1        ; xmm2: aaaa 9999 9988 8888 7777 7766 6666 5555
    pshufb xmm2, xmm3        ; xmm2: oooo oooo ooaa aa00 9999 9900 8888 8800
    ;
    movdqu xmm3, [rsp+16*4]
    movdqa xmm4, xmm0        ; xmm4: ffff ffee eeee dddd ddcc cccc bbbb bbaa
    pshufb xmm4, xmm3        ; xmm2: bbbb bboo aaoo oooo oooo oooo oooo oooo
    paddb  xmm2, xmm4        ; xmm2 := xmm2 + xmm4
    movdqa [r11+rdx], xmm2   ; store 4 qword data.
    add rdx, 16

    ; 4組目の4 PCM:                  ffff ffoo eeee eeoo dddd ddoo cccc ccoo
    movdqu xmm3, [rsp+16*5]
    pshufb xmm0, xmm3        ; xmm0: ffff ff00 eeee ee00 dddd dd00 cccc cc00
    movdqa [r11+rdx], xmm0   ; store 4 qword data.
    add rdx, 16

    add rcx, 16

    jnz LabelBegin
    RestoreRegisters
    ret
align 8
PCM24to32Asm endp
end

