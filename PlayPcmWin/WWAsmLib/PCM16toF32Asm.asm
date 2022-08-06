public PCM16toF32Asm

.code

; save不要のレジスタ: RAX RCX RDX R8 R9 R10 R11 XMM0 XMM1 XMM2 XMM3 XMM4 XMM5

; SSE2

; PCM16toF32Asm(const int16_t *src, float *dst, int64_t count)
; src      --> rcx
; dst      --> rdx
; count    --> r8
align 16
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

align 16

LoopBegin:

    ; 1ループで8個処理します。

    movdqu xmm1, [r10+rcx]   ; xmm1: 8 16bitPCM samples (total 16 bytes of data)

    pxor xmm0, xmm0          ; xmm0: all zero
    punpcklwd xmm0, xmm1     ; xmm0: 4 32bitPCM samples from lower 4 16bit samples of xmm1
    cvtdq2ps xmm0, xmm0      ; xmm0: 4 float values from signed int values.
    mulps xmm0, xmm3         ; xmm0 = xmm0 * xmm3, scale float value to [-1 1)
    movntdq [r11+rcx*2], xmm0 ; store 4 32bitPCM to dst memory

    pxor xmm0, xmm0          ; xmm0: all zero
    punpckhwd xmm0, xmm1     ; xmm0: 4 32bitPCM samples from higher 4 16bit samples of xmm1
    cvtdq2ps xmm0, xmm0      ; xmm0: 4 float values from signed int values.
    mulps xmm0, xmm3         ; xmm0 = xmm0 * xmm3, scale float value to [-1 1)
    movntdq [r11+rcx*2+16], xmm0 ; store 4 32bitPCM to dst memory

    add rcx, 16              ; move src pointer

    jnz LoopBegin            ; if rcx != 0 then jump

    ret

align 16
PCM16toF32Asm endp
end
