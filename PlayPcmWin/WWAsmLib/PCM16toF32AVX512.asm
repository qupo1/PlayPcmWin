public PCM16toF32AVX512

STACKBYTES    equ 16

.data
align 16

;     LSB      MSB
; zmm2: 014589cd
; zmm3: 2367abef

; 01234567
; 89abcdef

mask0   DQ 0000000000000000H
mask0_1 DQ 0000000000000001H
mask0_2 DQ 0000000000000008H
mask0_3 DQ 0000000000000009H
mask0_4 DQ 0000000000000002H
mask0_5 DQ 0000000000000003H
mask0_6 DQ 000000000000000aH
mask0_7 DQ 000000000000000bH

mask1   DQ 0000000000000004H
mask1_1 DQ 0000000000000005H
mask1_2 DQ 000000000000000cH
mask1_3 DQ 000000000000000dH
mask1_4 DQ 0000000000000006H
mask1_5 DQ 0000000000000007H
mask1_6 DQ 000000000000000eH
mask1_7 DQ 000000000000000fH

; 定数1.0f/(32768.0f* 65536.0f)が8個。
PCM32toPCM32F  DQ 3000000030000000H
PCM32toPCM32Fb DQ 3000000030000000H
PCM32toPCM32Fc DQ 3000000030000000H
PCM32toPCM32Fd DQ 3000000030000000H
PCM32toPCM32Fe DQ 3000000030000000H
PCM32toPCM32Ff DQ 3000000030000000H
PCM32toPCM32Fg DQ 3000000030000000H
PCM32toPCM32Fh DQ 3000000030000000H

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

; AVX512F,BW

; PCM16toF32AVX512(const char *src, int *dst, int64_t count)
; src      --> rcx
; dst      --> rdx
; count    --> r8
align 16
PCM16toF32AVX512 proc frame
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

    vpxorq zmm0, zmm0, zmm0 ; zmm0: all zero

align 16
LoopBegin:

    ; 1ループで32個処理します。

    vmovdqu64  zmm1, [rsi + rcx]; zmm1: 32 16bitPCM samples (total 64 bytes of data)

    vpunpcklwd zmm2, zmm0, zmm1 ; zmm2: 16 32bitPCM samples from lower 16 16bit samples of zmm1
    vpunpckhwd zmm3, zmm0, zmm1 ; zmm3: 16 32bitPCM samples from higher 16 16bit samples of zmm1

    vmovdqu64  zmm4, zmmword ptr mask0

    vpermi2q   zmm4, zmm2, zmm3

    ; 符号付きdword→float変換。
    vcvtdq2ps  zmm4, zmm4                        ; convert signed dword to float
    vmovdqu64  zmm1, zmmword ptr PCM32toPCM32F   ; zmm1 : 1.0f / (32768.0f * 65536.0f)を4個置く。
    vmulps     zmm4, zmm4, zmm1                  ; zmm4 := zmm4 * zmm1, scale float value to [-1 1)

    vmovntdq   zmmword ptr [rdi + rcx*2], zmm4   ; 16サンプル出力。

    vmovdqu64  zmm4, zmmword ptr mask1

    vpermi2q   zmm4, zmm2, zmm3

    ; 符号付きdword→float変換。
    vcvtdq2ps  zmm4, zmm4                         ; convert signed dword to float
    vmulps     zmm4, zmm4, zmm1                   ; zmm4 := zmm4 * zmm1, scale float value to [-1 1)

    vmovntdq   zmmword ptr [rdi + rcx*2+64], zmm4 ; 16サンプル出力。

    add rcx, 64   ; move src pointer

    jnz LoopBegin ; if rcx != 0 then jump

    RestoreRegisters
    vzeroupper
    ret

align 16
PCM16toF32AVX512 endp
end

