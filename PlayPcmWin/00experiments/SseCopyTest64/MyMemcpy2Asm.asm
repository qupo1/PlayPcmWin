; This code may be slower than memcpy()

public MyMemcpy2Asm

STACKBYTES    equ 16*3

.code

SaveRegisters MACRO
    sub rsp,STACKBYTES
   .allocstack STACKBYTES
    movdqu [rsp+16*0],xmm6
   .savexmm128 xmm6, 16*0
    movdqu [rsp+16*1],xmm7
   .savexmm128 xmm7, 16*1
    mov [rsp+16*2],rsi
   .savereg rsi,16*2
    mov [rsp+16*2+8],rdi
   .savereg rdi,16*2+8
   .endprolog
ENDM
 
RestoreRegisters MACRO
    movdqu xmm6, [rsp+16*0]
    movdqu xmm7, [rsp+16*1]
    mov rsi, [rsp+16*2]
    mov rdi, [rsp+16*2+8]
    add rsp,STACKBYTES
ENDM
 
; MyMemcpy2Asm(uint8_t *dst, const uint8_t *src, int64_t bytes)
 ; dst   --> rcx
 ; src   --> rdx
 ; bytes --> r8
 align 8
 MyMemcpy2Asm proc frame
    SaveRegisters

     mov rsi, rdx ; rsi: src pointer
     add rsi, r8  ; rsi: last of src

     mov rdi, rcx ; rdi: dest pointer
     add rdi, r8  ; rdi: last of dst

     mov rcx, r8  ; rcx: copy bytes arg
     neg rcx      ; rcx: negative copy bytes, now rsi+rcx points the start of src

 align 16
 LabelBegin:
     movdqu  xmm0,            [rsi + rcx]
     movdqu  xmm1,            [rsi + rcx +16]
     movdqu  xmm2,            [rsi + rcx +32]
     movdqu  xmm3,            [rsi + rcx +48]
     movdqu  xmm4,            [rsi + rcx +64]
     movdqu  xmm5,            [rsi + rcx +80]
     movdqu  xmm6,            [rsi + rcx +96]
     movdqu  xmm7,            [rsi + rcx +112]
     movntdq [rdi + rcx],     xmm0
     movntdq [rdi + rcx +16], xmm1
     movntdq [rdi + rcx +32], xmm2
     movntdq [rdi + rcx +48], xmm3
     movntdq [rdi + rcx +64], xmm4
     movntdq [rdi + rcx +80], xmm5
     movntdq [rdi + rcx +96], xmm6
     movntdq [rdi + rcx +112], xmm7

     add rcx, 128

     jnz LabelBegin

     RestoreRegisters
     ret
 align 8
 MyMemcpy2Asm endp
 end