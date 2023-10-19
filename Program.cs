using System.ComponentModel;

var estimatedCycles = 0;
var simulatedMemory = new byte[ushort.MaxValue];

// Path to the 8086 assembly that we'll run, usually assembled through the NASM assembler
var assemblyProgram = File.ReadAllBytes("../../../assembly");
Array.Copy(assemblyProgram, simulatedMemory, assemblyProgram.Length);

var bitsToRegisterW0 = new[]
{
    "al",
    "cl",
    "dl",
    "bl",
    "ah",
    "ch",
    "dh",
    "bh"
};
var bitsToRegisterW1 = new[]
{
    "ax",
    "cx",
    "dx",
    "bx",
    "sp",
    "bp",
    "si",
    "di"
};

var displacementPrefix = new[]
{
    "bx + si",
    "bx + di",
    "bp + si",
    "bp + di",
    "si",
    "di",
    "bp",
    "bx"
};

var registerValues = new short[8];
bool zFlag = false, sFlag = false;

string GetName(int register, bool wFlag)
{
    return wFlag ? bitsToRegisterW1[register] : bitsToRegisterW0[register];
}

short GetDisplacementValue(int rmField)
{
    return rmField switch
    {
        0 => (short)(registerValues[3] + registerValues[6]),
        1 => (short)(registerValues[3] + registerValues[7]),
        2 => (short)(registerValues[5] + registerValues[6]),
        3 => (short)(registerValues[5] + registerValues[7]),
        4 => registerValues[6],
        5 => registerValues[7],
        6 => registerValues[5],
        7 => registerValues[3],
        _ => throw new ArgumentException()
    };
}

int GetEffectiveAddressCycleCount(int rmField, bool hasDisplacement)
{
    if (hasDisplacement)
    {
        return rmField switch
        {
            0 => 11,
            1 => 12,
            2 => 12,
            3 => 11,
            4 => 9,
            5 => 9,
            6 => 9,
            7 => 9,
            _ => throw new ArgumentException()
        };
    }
    else
    {
        return rmField switch
        {
            0 => 7,
            1 => 8,
            2 => 8,
            3 => 7,
            4 => 5,
            5 => 5,
            6 => 5,
            7 => 5,
            _ => throw new ArgumentException()
        };
    }
}

void RegMemoryWithRegisterToEither(Instruction instruction, int mod, bool dFlag, int regField, bool wFlag, int rmField, ref int i)
{
    switch (mod)
    {
        case 0b11:
        {
            int destinationOperand, sourceOperand;

            if (dFlag)
            {
                destinationOperand = regField;
                sourceOperand = rmField;
            }
            else
            {
                destinationOperand = rmField;
                sourceOperand = regField;
            }

            switch (instruction)
            {
                case Instruction.mov:
                    estimatedCycles += 2;
                    registerValues[destinationOperand] = registerValues[sourceOperand];
                    break;
                case Instruction.add:
                    estimatedCycles += 3;
                    registerValues[destinationOperand]+= registerValues[sourceOperand];
                    SetFlags(registerValues[destinationOperand]);
                    break;
                case Instruction.sub:
                    registerValues[destinationOperand]-= registerValues[sourceOperand];
                    SetFlags(registerValues[destinationOperand]);
                    break;
                case Instruction.cmp:
                    SetFlags((short)(registerValues[destinationOperand] - registerValues[sourceOperand]));
                    break;
                default:
                    throw new InvalidEnumArgumentException();
            }
            i += 2;
            break;
        }
        case 0b01:
        {
            var baseDisplacement = GetDisplacementValue(rmField);
            var finalDisplacement = baseDisplacement + (sbyte)simulatedMemory[i + 2];

            if (dFlag)
            {
                switch (instruction)
                {
                    case Instruction.mov:
                        if (rmField == 6 && simulatedMemory[i + 2] == 0)
                            estimatedCycles += 8 + 5;
                        else 
                            estimatedCycles += 8 + GetEffectiveAddressCycleCount(rmField, true);
                        registerValues[regField] = BitConverter.ToInt16(simulatedMemory.AsSpan(finalDisplacement, 2));
                        break;
                    default:
                        throw new InvalidEnumArgumentException();
                }
            }
            else
            {
                switch (instruction)
                {
                    case Instruction.mov:
                        estimatedCycles += 9 + GetEffectiveAddressCycleCount(rmField, true);
                        BitConverter.TryWriteBytes(simulatedMemory.AsSpan(finalDisplacement, 2), registerValues[regField]);
                        break;
                    default:
                        throw new InvalidEnumArgumentException();
                }
            }

            i += 3;
            break;
        }
        case 0b10:
        {
            var baseDisplacement = GetDisplacementValue(rmField);
            var displacement16 = BitConverter.ToInt16(simulatedMemory.AsSpan(i + 2, 2));
            var finalDisplacement = baseDisplacement + displacement16;

            if (dFlag)
            {
                switch (instruction)
                {
                    case Instruction.mov:
                        estimatedCycles += 8 + GetEffectiveAddressCycleCount(rmField, true);
                        registerValues[regField] = BitConverter.ToInt16(simulatedMemory.AsSpan(finalDisplacement, 2));
                        break;
                    default:
                        throw new InvalidEnumArgumentException();
                }
            }
            else
            {
                switch (instruction)
                {
                    case Instruction.mov:
                        estimatedCycles += 9 + GetEffectiveAddressCycleCount(rmField, true);
                        BitConverter.TryWriteBytes(simulatedMemory.AsSpan(finalDisplacement, 2), registerValues[regField]);
                        break;
                    case Instruction.add:
                        estimatedCycles += 16 + GetEffectiveAddressCycleCount(rmField, true);
                        BitConverter.TryWriteBytes(simulatedMemory.AsSpan(finalDisplacement, 2), BitConverter.ToInt16(simulatedMemory.AsSpan(finalDisplacement, 2)) + registerValues[regField]);
                        break;
                    default:
                        throw new InvalidEnumArgumentException();
                }
            }

            i += 4;
            break;
        }
        default:
        {
            if (rmField == 0b110)
            {
                var displacement16 = BitConverter.ToInt16(simulatedMemory.AsSpan(i + 2, 2));

                if (dFlag)
                {
                    switch (instruction)
                    {
                        case Instruction.mov:
                            estimatedCycles += 8 + 6;
                            registerValues[regField] = BitConverter.ToInt16(simulatedMemory.AsSpan(displacement16, 2));
                            break;
                        default:
                            throw new InvalidEnumArgumentException();
                    }
                }
                else
                {
                    switch (instruction)
                    {
                        case Instruction.mov:
                            estimatedCycles += 9 + 6;
                            BitConverter.TryWriteBytes(simulatedMemory.AsSpan(displacement16, 2), registerValues[regField]);
                            break;
                        default:
                            throw new InvalidEnumArgumentException();
                    }
                }

                i += 4;
            }
            else
            {
                var displacement = GetDisplacementValue(rmField);

                if (dFlag)
                {
                    switch (instruction)
                    {
                        case Instruction.mov:
                            estimatedCycles += 8 + GetEffectiveAddressCycleCount(rmField, false);
                            registerValues[regField] = BitConverter.ToInt16(simulatedMemory.AsSpan(displacement, 2));
                            break;
                        default:
                            throw new InvalidEnumArgumentException();
                    }
                }
                else
                {
                    switch (instruction)
                    {
                        case Instruction.mov:
                            estimatedCycles += 9 + GetEffectiveAddressCycleCount(rmField, false);
                            BitConverter.TryWriteBytes(simulatedMemory.AsSpan(displacement, 2), registerValues[regField]);
                            break;
                        default:
                            throw new InvalidEnumArgumentException();
                    }
                }

                i += 2;
            }

            break;
        }
    }
}

void SetFlags(short result)
{
    zFlag = result == 0;
    sFlag = result < 0;
}

void ImmediateToRegisterMemory(Instruction instruction, int mod, bool dFlag, bool wFlag, bool wideData, int rmField, ref int i1)
{
    switch (mod)
    {
        case 0b11:
        {
            short data;
            if (wideData)
            {
                data = BitConverter.ToInt16(simulatedMemory.AsSpan(i1 + 2, 2));
                i1 += 4;
            }
            else
            {
                data = simulatedMemory[i1 + 2];
                i1 += 3;
            }

            if (dFlag)
            {
                switch (instruction)
                {
                    case Instruction.mov:
                        estimatedCycles += 4;
                        registerValues[rmField] = data;
                        break;
                    case Instruction.add:
                        estimatedCycles += 4;
                        registerValues[rmField]+= data;
                        SetFlags(registerValues[rmField]);
                        break;
                    case Instruction.sub:
                        registerValues[rmField]-= data;
                        SetFlags(registerValues[rmField]);
                        break;
                    case Instruction.cmp:
                        SetFlags((short)(registerValues[rmField] - data));
                        break;
                    default:
                        throw new InvalidEnumArgumentException();
                }
            }
            else
            {
                switch (instruction)
                {
                    case Instruction.mov:
                        BitConverter.TryWriteBytes(simulatedMemory.AsSpan(data, 2), registerValues[rmField]);
                        break;
                    default:
                        throw new InvalidEnumArgumentException();
                }
            }
            break;
        }
        case 0b01:
        {
            short baseDisplacement = GetDisplacementValue(rmField);
            var additionalDisplacement = simulatedMemory[i1 + 2];

            short data;
            if (wideData)
            {
                data = BitConverter.ToInt16(simulatedMemory.AsSpan(i1 + 3, 2));
                i1 += 5;
            }
            else
            {
                data = simulatedMemory[i1 + 3];
                i1 += 4;
            }

            if (dFlag)
            {
                switch (instruction)
                {
                    case Instruction.mov:
                        BitConverter.TryWriteBytes(simulatedMemory.AsSpan(baseDisplacement + additionalDisplacement, 2), data);
                        break;
                    default:
                        throw new ArgumentException();
                }
            }
            else
            {
                switch (instruction)
                {
                    default:
                        throw new ArgumentException();
                }
            }
            break;
        }
        case 0b10:
        {
            var displacement16 = BitConverter.ToInt16(simulatedMemory.AsSpan(i1 + 2, 2));
            short baseDisplacement = GetDisplacementValue(rmField);

            short data;
            if (wideData)
            {
                data = BitConverter.ToInt16(simulatedMemory.AsSpan(i1 + 4, 2));
                i1 += 6;
            }
            else
            {
                data = simulatedMemory[i1 + 4];
                i1 += 5;
            }

            if (dFlag)
            {
                switch (instruction)
                {
                    case Instruction.mov:
                        BitConverter.TryWriteBytes(simulatedMemory.AsSpan(baseDisplacement + displacement16, 2), data);
                        break;
                    default:
                        throw new ArgumentException();
                }
            }
            else
            {
                switch (instruction)
                {
                    default:
                        throw new ArgumentException();
                }
            }
            break;
        }
        default:
        {
            short data;
            if (rmField == 0b110)
            {
                var displacement16 = BitConverter.ToInt16(simulatedMemory.AsSpan(i1 + 2, 2));

                if (wideData)
                {
                    data = BitConverter.ToInt16(simulatedMemory.AsSpan(i1 + 4, 2));
                    i1 += 6;
                }
                else
                {
                    data = simulatedMemory[i1 + 4];
                    i1 += 5;
                }

                if (dFlag)
                {
                    switch (instruction)
                    {
                        case Instruction.mov:
                            BitConverter.TryWriteBytes(simulatedMemory.AsSpan(displacement16, 2), data);
                            break;
                        default:
                            throw new ArgumentException();
                    }
                }
                else
                {
                    switch (instruction)
                    {
                        default:
                            throw new ArgumentException();
                    }
                }
            }
            else
            {
                short displacement = GetDisplacementValue(rmField);

                if (wideData)
                {
                    data = BitConverter.ToInt16(simulatedMemory.AsSpan(i1 + 2, 2));
                    i1 += 4;
                }
                else
                {
                    data = simulatedMemory[i1 + 2];
                    i1 += 3;
                }

                if (dFlag)
                {
                    switch (instruction)
                    {
                        case Instruction.mov:
                            BitConverter.TryWriteBytes(simulatedMemory.AsSpan(displacement, 2), data);
                            break;
                        default:
                            throw new InvalidEnumArgumentException();
                    }
                }
                else
                {
                    switch (instruction)
                    {
                        default:
                            throw new InvalidEnumArgumentException();
                    }
                }
            }
            break;
        }
    }
}

void ImmediateToAccumulator(string instructionName, bool wFlag, ref int i)
{
    short data;
    if (wFlag)
    {
        data = BitConverter.ToInt16(simulatedMemory.AsSpan(i + 1, 2));
        i += 3;
    }
    else
    {
        data = (sbyte)simulatedMemory[i + 1];
        i += 2;
    }

    Console.WriteLine($"{instructionName} {GetName(0, wFlag)}, {data}");
}

void PrintState()
{
    Console.WriteLine("Values:");
    for (int i = 0; i < registerValues.Length; i++)
        Console.WriteLine($"{bitsToRegisterW1[i]} {(ushort)registerValues[i]}");

    Console.WriteLine($"zFlag: {zFlag} sFlag: {sFlag}");
}

for (int i = 0; i < assemblyProgram.Length;)
{
    var firstByte = simulatedMemory[i];
    var secondByte = simulatedMemory[i + 1];
    var wFlag = (firstByte & 1) == 1;
    var mod = secondByte >> 6;
    var regField = (secondByte >> 3) & 0b111;
    var rmField = secondByte & 0b111;
    var dFlag = (firstByte & 0b10) == 0b10;

    switch (firstByte)
    {
        case 0b10001000:
        case 0b10001001:
        case 0b10001010:
        case 0b10001011:
        {
            RegMemoryWithRegisterToEither(Instruction.mov, mod, dFlag, regField, wFlag, rmField, ref i);
            break;
        }
        case 0b00000000:
        case 0b00000001:
        case 0b00000010:
        case 0b00000011:
        {
            RegMemoryWithRegisterToEither(Instruction.add, mod, dFlag, regField, wFlag, rmField, ref i);
            break;
        }
        case 0b00101000:
        case 0b00101001:
        case 0b00101010:
        case 0b00101011:
        {
            RegMemoryWithRegisterToEither(Instruction.sub, mod, dFlag, regField, wFlag, rmField, ref i);
            break;
        }
        case 0b00111000:
        case 0b00111001:
        case 0b00111010:
        case 0b00111011:
        {
            RegMemoryWithRegisterToEither(Instruction.cmp, mod, dFlag, regField, wFlag, rmField, ref i);
            break;
        }
        case 0b11000110:
        case 0b11000111:
        {
            ImmediateToRegisterMemory(Instruction.mov, mod, dFlag, wFlag, wFlag, rmField, ref i);
            break;
        }
        case 0b10000000:
        case 0b10000001:
        case 0b10000010:
        case 0b10000011:
        {
            bool wideData = wFlag && !dFlag;
            switch (regField)
            {
                case 0:
                    ImmediateToRegisterMemory(Instruction.add, mod, true, wFlag, wideData, rmField, ref i);
                    break;
                case 0b101:
                    ImmediateToRegisterMemory(Instruction.sub, mod, true, wFlag, wideData, rmField, ref i);
                    break;
                case 0b111:
                    ImmediateToRegisterMemory(Instruction.cmp, mod, true, wFlag, wideData, rmField, ref i);
                    break;
                default:
                    throw new ArgumentException(nameof(regField));
            }
            break;
        }
        case 0b00000100:
        case 0b00000101:
        {
            ImmediateToAccumulator("add", wFlag, ref i);
            break;
        }
        case 0b00101100:
        case 0b00101101:
        {
            ImmediateToAccumulator("sub", wFlag, ref i);
            break;
        }
        case 0b00111100:
        case 0b00111101:
        {
            ImmediateToAccumulator("cmp", wFlag, ref i);
            break;
        }
        case 0b10110000:
        case 0b10110001:
        case 0b10110010:
        case 0b10110011:
        case 0b10110100:
        case 0b10110101:
        case 0b10110110:
        case 0b10110111:
        case 0b10111000:
        case 0b10111001:
        case 0b10111010:
        case 0b10111011:
        case 0b10111100:
        case 0b10111101:
        case 0b10111110:
        case 0b10111111:
        {
            wFlag = (firstByte & 0b00001000) == 0b00001000;
            regField = firstByte & 0b111;
            short data;
            if (wFlag)
            {
                data = BitConverter.ToInt16(simulatedMemory.AsSpan(i + 1, 2));
                i += 3;
            }
            else
            {
                data = simulatedMemory[i + 1];
                i += 2;
            }

            estimatedCycles += 4;
            registerValues[regField] = data;
            break;
        }
        case 0b10100000:
        case 0b10100001:
        {
            var address = BitConverter.ToInt16(simulatedMemory.AsSpan(i + 1, 2));
            Console.WriteLine($"mov ax, [{address}]");

            i += 3;
            break;
        }
        case 0b10100010:
        case 0b10100011:
        {
            var address = BitConverter.ToInt16(simulatedMemory.AsSpan(i + 1, 2));
            Console.WriteLine($"mov [{address}], ax");

            i += 3;
            break;
        }
        case 0b01110100:
            Console.WriteLine($"je {(sbyte)secondByte}");
            i+= 2;
            break;
        case 0b01111100:
            Console.WriteLine($"jl {(sbyte)secondByte}");
            i+= 2;
            break;
        case 0b01111110:
            Console.WriteLine($"jle {(sbyte)secondByte}");
            i+= 2;
            break;
        case 0b01110010:
            Console.WriteLine($"jb {(sbyte)secondByte}");
            i+= 2;
            break;
        case 0b01110110:
            Console.WriteLine($"jbe {(sbyte)secondByte}");
            i+= 2;
            break;
        case 0b01111010:
            Console.WriteLine($"jp {(sbyte)secondByte}");
            i+= 2;
            break;
        case 0b01110000:
            Console.WriteLine($"jo {(sbyte)secondByte}");
            i+= 2;
            break;
        case 0b01111000:
            Console.WriteLine($"js {(sbyte)secondByte}");
            i+= 2;
            break;
        case 0b01110101:
            i+= 2;
            if (zFlag == false)
                i += (sbyte)secondByte;
            break;
        case 0b01111101:
            Console.WriteLine($"jnl {(sbyte)secondByte}");
            i+= 2;
            break;
        case 0b01111111:
            Console.WriteLine($"jg {(sbyte)secondByte}");
            i+= 2;
            break;
        case 0b01110011:
            Console.WriteLine($"jnb {(sbyte)secondByte}");
            i+= 2;
            break;
        case 0b01110111:
            Console.WriteLine($"ja {(sbyte)secondByte}");
            i+= 2;
            break;
        case 0b01111011:
            Console.WriteLine($"jnp {(sbyte)secondByte}");
            i+= 2;
            break;
        case 0b01110001:
            Console.WriteLine($"jno {(sbyte)secondByte}");
            i+= 2;
            break;
        case 0b01111001:
            Console.WriteLine($"jns {(sbyte)secondByte}");
            i+= 2;
            break;
        case 0b11100010:
            Console.WriteLine($"loop {(sbyte)secondByte}");
            i+= 2;
            break;
        case 0b11100001:
            Console.WriteLine($"loopz {(sbyte)secondByte}");
            i+= 2;
            break;
        case 0b11100000:
            Console.WriteLine($"loopnz {(sbyte)secondByte}");
            i+= 2;
            break;
        case 0b11100011:
            Console.WriteLine($"jcxz {(sbyte)secondByte}");
            i+= 2;
            break;
        default:
            throw new InvalidOperationException("Invalid byte");
    }

    Console.WriteLine("Estimated cycles so far: " + estimatedCycles);
}

PrintState();

File.WriteAllBytes("image.data", simulatedMemory.AsSpan(256, 16384).ToArray());


enum Instruction
{
    mov,
    add,
    sub,
    cmp
}