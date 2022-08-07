STACKBYTES    equ 16

.data
align 16

; 定数1.0f/(32768.0f* 65536.0f)が8個。
PCM32toPCM32F  DQ 3000000030000000H
PCM32toPCM32Fb DQ 3000000030000000H
PCM32toPCM32Fc DQ 3000000030000000H
PCM32toPCM32Fd DQ 3000000030000000H

public PCM16toF32AVX

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

; save不要のレジスタ: RAX RCX RDX R8 R9 R10 R11 XMM0 XMM1 XMM2 XMM3 XMM4 XMM5

; AVX2

; PCM16toF32AVX(const char *src, int *dst, int64_t count)
; src      --> rcx
; dst      --> rdx
; count    --> r8
align 16
PCM16toF32AVX proc frame
    SaveRegisters
   
    ; dstBytesを算出し、srcバッファ、dstバッファの終わりのアドレスを算出。

    mov rsi, rcx  ; rsi: src address
    mov rdi, rdx  ; rdi: dst address

    mov rax, r8   ; calc srcBytes, that is count*2
    mov rcx, 2    ; rcx := 2
    mul rcx       ; rax: srcBytes , rdx:rax := rax * 2
    mov r9, rax   ; r9: srcBytes
    add rsi, rax  ; now rsi points the end of src buffer

    mul rcx       ; rax: dstBytes = count*4, rdx:rax := rax * 2
    add rdi, rax  ; now rdi points the end of dst buffer

    mov rcx, r9   ; rcx: srcBytes
    neg rcx       ; now rsi+rcx points the start of the src buffer
                  ; and rdi+rcx*2 points the start of the dst buffer

align 16
LoopBegin:

    ; 1ループで16個処理します。

    vmovdqu    ymm1, ymmword ptr [rsi + rcx] ; ymm1: 16 16bitPCM samples (total 32 bytes of data)

    vpxor ymm0, ymm0, ymm0 ; ymm0: all zero
    vpunpcklwd ymm2, ymm0, ymm1  ; ymm2: 8 32bitPCM samples from lower 8 16bit samples of ymm1
    vpunpckhwd ymm3, ymm0, ymm1  ; ymm3: 8 32bitPCM samples from higher 8 16bit samples of ymm1

    ; 下半分8個作成。
    ;     LSB      MSB
    ; ymm2: 012389ab
    ; ymm3: 4567cdef
    ; ↓
    ; ymm4: 01234567
    ;
    ; imm8 1:0 = 0 (ymm2L)
    ; imm8 5:4 = 2 (ymm3L)
    ; imm8 3   = 0 (copy)
    ; imm8 7   = 0 (copy)
    ; imm8 = 00100000
    vperm2i128 ymm4, ymm2, ymm3, 020H

    ; 符号付きdword→float変換。
    vcvtdq2ps  ymm4, ymm4                        ; convert signed dword to float
    vmovdqu    ymm0, ymmword ptr PCM32toPCM32F   ; ymm0 : 1.0f / (32768.0f * 65536.0f)を4個置く。
    vmulps     ymm4, ymm4, ymm0                  ; ymm4 := ymm4 * ymm0, scale float value to [-1 1)

    vmovntdq   ymmword ptr [rdi + rcx*2], ymm4   ; 8個出力。

    ; 上半分8個作成。
    ;     LSB      MSB
    ; ymm2: 012389ab
    ; ymm3: 4567cdef
    ; ↓
    ; ymm4: 89abcdef
    ;
    ; imm8 1:0 = 1 (ymm2H)
    ; imm8 5:4 = 3 (ymm3H)
    ; imm8 3   = 0 (copy)
    ; imm8 7   = 0 (copy)
    ; imm8 = 00110001
    vperm2i128 ymm4, ymm2, ymm3, 031H

    ; 符号付きdword→float変換。
    vcvtdq2ps  ymm4, ymm4                         ; convert signed dword to float
    vmulps     ymm4, ymm4, ymm0                   ; ymm4 := ymm4 * ymm0, scale float value to [-1 1)

    vmovntdq   ymmword ptr [rdi + rcx*2+32], ymm4 ; 8個出力。

    add rcx, 32   ; move src pointer

    jnz LoopBegin ; if rcx != 0 then jump

    RestoreRegisters
    vzeroupper
    ret

align 16
PCM16toF32AVX endp
end

