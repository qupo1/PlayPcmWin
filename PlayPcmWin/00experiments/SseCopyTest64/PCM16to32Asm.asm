public PCM16to32Asm

.code

; save不要のレジスタ: RAX RCX RDX R8 R9 R10 R11 XMM0 XMM1 XMM2 XMM3 XMM4 XMM5

; PCM16to32Asm(const char *src, int *dst, int64_t count)
; src      --> rcx
; dst      --> rdx
; count    --> r8
align 8
PCM16to32Asm proc frame
    .endprolog
   
    ; dstBytesを算出し、srcバッファ、dstバッファの終わりのアドレスを算出。

    mov r10, rcx  ; r10: src address
    mov r11, rdx  ; r11: dst address

    mov rax, r8   ; calc srcBytes, that is count*2
    mov rcx, 2    ; rcx := 2
    mul rcx       ; rax: srcBytes , rdx:rax := rax * 2
    mov r9, rax   ; r9: srcBytes
    add r10, rax  ; now r10 points the end of src buffer

    mul rcx       ; rax: dstBytes = count*4, rdx:rax := rax * 2
    add r11, rax  ; now r11 points the end of dst buffer

    mov rcx, r9   ; rcx: srcBytes
    neg rcx       ; now r10+rcx points the start of the src buffer
                  ; and r11+rcx*2 points the start of the dst buffer

align 8
LoopBegin:
    movdqa xmm1, [r10+rcx]   ; xmm1: 8 16bitPCM samples (total 16 bytes of data)

    pxor xmm0, xmm0          ; xmm0: all zero
    punpcklwd xmm0, xmm1     ; xmm0: 4 32bitPCM samples from lower 4 16bit samples of xmm1
    movdqa [r11+rcx*2], xmm0 ; store 4 32bitPCM to dst memory

    pxor xmm0, xmm0          ; xmm0: all zero
    punpckhwd xmm0, xmm1     ; xmm0: 4 32bitPCM samples from higher 4 16bit samples of xmm1
    movdqa [r11+rcx*2+16], xmm0 ; store 4 32bitPCM to dst memory

    add rcx, 16              ; move src pointer

    jnz LoopBegin            ; if rcx != 0 then jump

    ret
align 8
PCM16to32Asm endp
end

