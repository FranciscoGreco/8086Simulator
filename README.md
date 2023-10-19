# 8086Simulator

8086 Simulator that can simulate and estime the cycle count for mov, add, sub, and cmp instructions as if they were running on an Intel 8086. 
ZF and SF flags are supported.

Meaning for the terminology used in the source code can be found in the 8086 Intel manual:
https://edge.edx.org/c4x/BITSPilani/EEE231/asset/8086_family_Users_Manual_1_.pdf

It can also decode most jump instructions (je, jl, jle, etc) plus a few additional instructions.

This isn't a complete 8086 simulator, as it was just part of the homework from Casey Muratori's Computer Enhance course: https://www.computerenhance.com which only involves the most basic 8086 instructions. 
