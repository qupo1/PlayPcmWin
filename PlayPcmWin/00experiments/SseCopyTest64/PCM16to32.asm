public PCM16to32

.code

; save不要のレジスタ: RAX RCX RDX R8 R9 R10 R11 XMM0 XMM1 XMM2 XMM3 XMM4 XMM5

; PCM16to32(const short *src, int *dst, int64_t count)
; src      --> rcx
; dst      --> rdx
; count    --> r8
align 8
PCM16to32 proc frame
   .endprolog
    ; dstBytesを算出し、dstバッファ、srcバッファの終わりのアドレスを算出。
    mov r10, rcx  ; r10: src address
    mov r11, rdx  ; r11: dst address
    ;
    mov rax, r8   ; calc srcBytes, that is count*4
    mov rcx, 2    ;
    mul rcx       ; rax: srcBytes , rdx:rax := rax * 2
    mov r9, rax   ; r9: srcBytes
    add r10, rax  ; now r10 points the end of src buffer
    ;
    mul rcx       ; rax: dstBytes , rdx:rax := rax * 2
    add r11, rax  ; now r11 points the end of dst buffer
    ;
    mov rcx, r9   ; rcx: srcBytes
    neg rcx       ; now r10+rcx points the start of the src buffer and r11+rcx*2 points the start of the dst buffer
align 8
LabelBegin:
    ; src → dst packed move zero extention word → dword。
    movdqa xmm0, [r10+rcx]
    pmovzxwd xmm1, xmm0      ; rightmost 4 word data of xmm0 are expanded to 4 dword data on xmm1.
    pslld xmm1, 16           ; 16bit left shift.
    movdqa [r11+rcx*2], xmm1 ; store 4 qword data.
    add rcx, 8H
    psrldq xmm0, 8           ; shift right by 8 bytes to get upper half data of xmm0
    pmovzxwd xmm1, xmm0      ; 4 word → 4 dword.
    pslld xmm1, 16           ; 16bit left shift.
    movdqa [r11+rcx*2], xmm1 ; store 4 dword data.
    add rcx, 8H
    jnz LabelBegin
    ret
align 8
PCM16to32 endp
end

