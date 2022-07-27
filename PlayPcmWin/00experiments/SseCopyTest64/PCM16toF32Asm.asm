public PCM16toF32Asm

.code

; save不要のレジスタ: RAX RCX RDX R8 R9 R10 R11 XMM0 XMM1 XMM2 XMM3 XMM4 XMM5

; PCM16toF32Asm(const short *src, float *dst, int64_t count)
; src      --> rcx
; dst      --> rdx
; count    --> r8
align 8
PCM16toF32Asm proc frame
   .endprolog

    ; dstBytesを算出し、dstバッファ、srcバッファの終わりのアドレスを算出。
    mov r10, rcx  ; r10: src address
    mov r11, rdx  ; r11: dst address

    mov rax, r8   ; calc srcBytes, that is count*2
    mov rcx, 2    ;
    mul rcx       ; rax: srcBytes , rdx:rax := rax * 2
    mov r9, rax   ; r9: srcBytes
    add r10, rax  ; now r10 points the end of src buffer

    mul rcx       ; rax: dstBytes that is count*4, rdx:rax := rax * 2
    add r11, rax  ; now r11 points the end of dst buffer

    mov rcx, r9   ; rcx: srcBytes
    neg rcx       ; now r10+rcx points the start of the src buffer and r11+rcx*2 points the start of the dst buffer

    ; xmm3に1.0f / (32768.0f * 65536.0f)を4個置く。
    mov    rax,  30000000h
    movd   xmm3, rax
    shufps xmm3, xmm3, 0

align 8
LoopBegin:

    movdqa xmm0, [r10+rcx]   ; xmm0: 8 16bitPCM samples

    pmovzxwd xmm1, xmm0      ; rightmost 4 word data of xmm0 are expanded to 4 dword data on xmm1.
    pslld xmm1, 16           ; 16bit left shift to get 4 signed int values.
    cvtdq2ps xmm2, xmm1      ; xmm2: 4 float values from signed int values.
    mulps xmm2, xmm3         ; xmm2 = xmm2 * xmm3, scale float value to [-1 1)
    movdqa [r11 + rcx*2], xmm2 ; store 4 float data to memory.

    psrldq xmm0, 8           ; shift right by 8 bytes to get upper half data of xmm0
    pmovzxwd xmm1, xmm0      ; 4 word → 4 dword.
    pslld xmm1, 16           ; 16bit left shift.
    cvtdq2ps xmm2, xmm1      ; xmm2: 4 float values from signed int values.
    mulps xmm2, xmm3         ; xmm2 = xmm2 * xmm3, scale float value to [-1 1)
    movdqa [r11 + rcx*2 + 16], xmm2 ; store 4 float data to memory.

    add rcx, 16

    jnz LoopBegin

    ret

align 8
PCM16toF32Asm endp
end

