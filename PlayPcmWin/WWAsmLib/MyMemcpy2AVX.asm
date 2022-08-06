; This code may be slower than memcpy()

public MyMemcpy2AVX

STACKBYTES    equ 16

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
 
; MyMemcpy2AVX(uint8_t *dst, const uint8_t *src, int64_t bytes)
 ; dst   --> rcx
 ; src   --> rdx
 ; bytes --> r8
 align 8
 MyMemcpy2AVX proc frame
    SaveRegisters

     mov rsi, rdx ; rsi: src pointer
     add rsi, r8  ; rsi: last of src

     mov rdi, rcx ; rdi: dest pointer
     add rdi, r8  ; rdi: last of dst

     mov rcx, r8  ; rcx: copy bytes arg
     neg rcx      ; rcx: negative copy bytes, now rsi+rcx points the start of src

 align 16
 LabelBegin:
     vmovdqu  ymm0,                        ymmword ptr [rsi + rcx]
     vmovdqu  ymm1,                        ymmword ptr [rsi + rcx +32]
     vmovdqu  ymm2,                        ymmword ptr [rsi + rcx +64]
     vmovdqu  ymm3,                        ymmword ptr [rsi + rcx +96]
     vmovntdq ymmword ptr [rdi + rcx],     ymm0
     vmovntdq ymmword ptr [rdi + rcx +32], ymm1
     vmovntdq ymmword ptr [rdi + rcx +64], ymm2
     vmovntdq ymmword ptr [rdi + rcx +96], ymm3

     add rcx, 128

     jnz LabelBegin

     RestoreRegisters
     vzeroupper
     ret
 align 8
 MyMemcpy2AVX endp
 end