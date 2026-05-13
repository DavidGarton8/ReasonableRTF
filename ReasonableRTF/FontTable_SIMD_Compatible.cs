#if !NET8_0_OR_GREATER
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReasonableRTF.Enums;
using ReasonableRTF.Extensions;

namespace ReasonableRTF;

public sealed partial class RtfToTextConverter
{
    private readonly Vector<byte>[] _symbolFontNameVectors = new Vector<byte>[_symbolArraysLength];

    private void InitSymbolFontNameVectors()
    {
        Span<byte> bytes = stackalloc byte[Vector<byte>.Count];

        for (int i = _symbolArraysStartingIndex; i < _symbolArraysLength; i++)
        {
            _symbolFontNameVectors[i] = GetZeroPaddedVector(bytes, _symbolFontCharsArrays[i]);
        }

        return;

        static Vector<byte> GetZeroPaddedVector(Span<byte> bytes, byte[] name)
        {
            if (name.Length > Vector<byte>.Count)
            {
                return Vector<byte>.Zero;
            }

            bytes.Clear();
            name.CopyTo(bytes);

            return Unsafe.ReadUnaligned<Vector<byte>>(ref MemoryMarshal.GetReference(bytes));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SymbolFont SIMD_TryGetFontName(
        ref byte bufferRef,
        byte[] buffer,
        char ch,
        ref int currentPos)
    {
        currentPos--;

        Vector<byte> vector = Unsafe.ReadUnaligned<Vector<byte>>(ref GetRefAtCurrentPos(ref bufferRef));
        Vector<byte> equalsTerminatingChar =
            Vector.Equals(_zeroVector, vector) |
            Vector.Equals(_lfVector, vector) |
            Vector.Equals(_crVector, vector) |
            Vector.Equals(_backslashVector, vector) |
            Vector.Equals(_openBraceVector, vector) |
            Vector.Equals(_closingBraceVector, vector) |
            Vector.Equals(_semicolonVector, vector);

        if (equalsTerminatingChar != Vector<byte>.Zero)
        {
            int terminatingCharIndex = LocateFirstFoundByte(equalsTerminatingChar);
            ch = (char)vector[terminatingCharIndex];

            if (EarlyOut(terminatingCharIndex, ref currentPos, ch))
            {
                return SymbolFont.None;
            }

            Vector<byte> maskVec = Vector.GreaterThan(new Vector<byte>((byte)terminatingCharIndex), _indexVec);
            Vector<byte> fontName = Vector.BitwiseAnd(vector, maskVec);

            return TryFindSymbolFont(fontName, _symbolFontNameVectors, ref currentPos, ch, terminatingCharIndex);
        }
        else
        {
            ch = (char)GetByteAtPos(ref bufferRef, currentPos + Vector<byte>.Count);
            if (ch == ';' || _isNonPlainText[(byte)ch])
            {
                if (EarlyOut(Vector<byte>.Count, ref currentPos, ch))
                {
                    return SymbolFont.None;
                }

                return TryFindSymbolFont(vector, _symbolFontNameVectors, ref currentPos, ch, Vector<byte>.Count);
            }
            else
            {
                currentPos += Vector<byte>.Count;
                if (Vector<byte>.Count < _maxSupportedSymbolFontNameLength)
                {
                    vector.CopyTo(_symbolFontNameBuffer);
                    return GetSymbolFont_Scalar(ref bufferRef, ch, Vector<byte>.Count);
                }
                else
                {
                    return SymbolFont.None;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool EarlyOut(int index, ref int currentPos, char ch)
        {
            if (!index.IsBetween(_minSupportedSymbolFontNameLength, _maxSupportedSymbolFontNameLength) ||
                !_symbolFontNameLengths[index])
            {
                currentPos += ch == ';' ? index + 1 : index;
                return true;
            }
            else
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static SymbolFont TryFindSymbolFont(Vector<byte> fontName, Vector<byte>[] symbolFontNameVectors, ref int currentPos, char ch, int index)
        {
            for (int i = _symbolArraysStartingIndex; i < _symbolArraysLength; i++)
            {
                if (fontName == symbolFontNameVectors[i])
                {
                    currentPos += ch == ';' ? index + 1 : index;
                    return (SymbolFont)i;
                }
            }

            currentPos += ch == ';' ? index + 1 : index;
            return SymbolFont.None;
        }
    }
}
#endif
