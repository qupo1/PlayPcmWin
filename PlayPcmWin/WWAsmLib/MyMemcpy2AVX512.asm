; This code may be slower than memcpy()

public MyMemcpy2AVX512

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

; MyMemcpy2AVX512(uint8_t *dst, const uint8_t *src, int64_t bytes)
 ; dst   --> rcx
 ; src   --> rdx
 ; bytes --> r8
 align 8
 MyMemcpy2AVX512 proc frame
    SaveRegisters

     mov rsi, rdx ; rsi: src pointer
     add rsi, r8  ; rsi: last of src

     mov rdi, rcx ; rdi: dest pointer
     add rdi, r8  ; rdi: last of dst

     mov rcx, r8  ; rcx: copy bytes arg
     neg rcx      ; rcx: negative copy bytes, now rsi+rcx points the start of src

 align 16
 LabelBegin:
     vmovdqu64 zmm0,                        [rsi + rcx]
     vmovdqu64 zmm1,                        [rsi + rcx +64]
     vmovntdq  zmmword ptr [rdi + rcx],     zmm0
     vmovntdq  zmmword ptr [rdi + rcx +64], zmm1

     add rcx, 128

     jnz LabelBegin

     RestoreRegisters
     vzeroupper

     ret
 align 8
 MyMemcpy2AVX512 endp
 end