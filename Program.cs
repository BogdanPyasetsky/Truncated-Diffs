using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Diagnostics;

namespace Truncated_Diffs
{
    internal class Program
    {
        //////////// Midori64 and Midori128 ////////////

        private static int[] Sb0 = new int[16] { 0xc, 0xa, 0xd, 0x3, 0xe, 0xb, 0xf, 0x7, 0x8, 0x9, 0x1, 0x5, 0x0, 0x2, 0x4, 0x6 };
        private static int[] Sb1 = new int[16] { 0x1, 0x0, 0x5, 0x3, 0xe, 0x2, 0xf, 0x7, 0xd, 0xa, 0x9, 0xb, 0xc, 0x8, 0x4, 0x6 };

        private static int[,] beta = new int[19, 16] {
           { 0, 0, 0, 1,   0, 1, 0, 1,   1, 0, 1, 1,   0, 0, 1, 1 },
           { 0, 1, 1, 1,   1, 0, 0, 0,   1, 1, 0, 0,   0, 0, 0, 0 },
           { 1, 0, 1, 0,   0, 1, 0, 0,   0, 0, 1, 1,   0, 1, 0, 1 },
           { 0, 1, 1, 0,   0, 0, 1, 0,   0, 0, 0, 1,   0, 0, 1, 1 },
           { 0, 0, 0, 1,   0, 0, 0, 0,   0, 1, 0, 0,   1, 1, 1, 1 },
           { 1, 1, 0, 1,   0, 0, 0, 1,   0, 1, 1, 1,   0, 0, 0, 0 },
           { 0, 0, 0, 0,   0, 0, 1, 0,   0, 1, 1, 0,   0, 1, 1, 0 },
           { 0, 0, 0, 0,   1, 0, 1, 1,   1, 1, 0, 0,   1, 1, 0, 0 },
           { 1, 0, 0, 1,   0, 1, 0, 0,   1, 0, 0, 0,   0, 0, 0, 1 },
           { 0, 1, 0, 0,   0, 0, 0, 0,   1, 0, 1, 1,   1, 0, 0, 0 },
           { 0, 1, 1, 1,   0, 0, 0, 1,   1, 0, 0, 1,   0, 1, 1, 1 },
           { 0, 0, 1, 0,   0, 0, 1, 0,   1, 0, 0, 0,   1, 1, 1, 0 },
           { 0, 1, 0, 1,   0, 0, 0, 1,   0, 0, 1, 1,   0, 0, 0, 0 },
           { 1, 1, 1, 1,   1, 0, 0, 0,   1, 1, 0, 0,   1, 0, 1, 0 },
           { 1, 1, 0, 1,   1, 1, 1, 1,   1, 0, 0, 1,   0, 0, 0, 0 },
           { 0, 1, 1, 1,   1, 1, 0, 0,   1, 0, 0, 0,   0, 0, 0, 1 },
           { 0, 0, 0, 1,   1, 1, 0, 0,   0, 0, 1, 0,   0, 1, 0, 0 },
           { 0, 0, 1, 0,   0, 0, 1, 1,   1, 0, 1, 1,   0, 1, 0, 0 },
           { 0, 1, 1, 0,   0, 0, 1, 0,   1, 0, 0, 0,   1, 0, 1, 0 }
        };



        static void KeyGen64(string key_128bit, out int[] WK, out int[] K0, out int[] K1)
        {
            int n = key_128bit.Length / 2;

            int[] FHalf = new int[n];
            int[] SHalf = new int[n];
            int[]Sum = new int[n];

            for (int i = 0; i < n; i++)
            {
                FHalf[i] = Convert.ToInt32("0x" + key_128bit[i], 16);
                SHalf[i] = Convert.ToInt32("0x" + key_128bit[n + i], 16);
                Sum[i] = FHalf[i] ^ SHalf[i];
            }

            K0 = FHalf;
            K1 = SHalf;
            WK = Sum;

            return;
        }

        static int[] KeyGen128(string key_128bit)
        {
            int n = key_128bit.Length / 2;
            int[] WK = new int[n];

            for (int i = 0; i < n; i++)
            {
                WK[i] = Convert.ToInt32("0x" + key_128bit.Substring(2 * i, 2), 16);
            }
            return WK;
        }



        static int[] KeyAdd (int[] state, int[] key, int iteration)
        {
            if (iteration == -1)
            {
                for (int i = 0; i < 16; i++)
                    state[i] = state[i] ^ key[i];  
            }
            else
            {
                int[] k = new int[16];
                for (int i = 0; i < 16; i++)
                {
                    k[i] = key[i] ^ beta[iteration, i];
                    state[i] = k[i] ^ state[i];
                }
            }
            return state;
        }

        static int[] SubCell64(int[] State)
        {
            int[] NewState = new int[16];
            for (int i = 0; i < 16; i++)
            {
                NewState[i] = Sb0[State[i]];
            }
            return NewState;
        }

        static int SSb0 (int cell)
        {
            int[] newIdx = new int[8] { 4, 1, 6, 3, 0, 5, 2, 7 };
            int[] bit = new int[8];
            int[] newBit = new int[8];
            int tempCell = 0;
            int resCell = 0;
            int mask = 0x80;
            //converting two hexes to array of 8 bits
            for (int i = 0; i < 8; i++)
            {
                bit[i] = cell & mask;
                bit[i] = bit[i] >> (7 - i);
                mask = mask >> 1;
            }
            //applying pemutation
            for (int i = 0; i < 8; i++)
            {
                newBit[i] = bit[newIdx[i]];
            }
            //assemble bits to hexes
            for (int i = 0; i < 7; i++)
            {
                tempCell = tempCell ^ newBit[i];
                tempCell = tempCell << 1;
            }
            tempCell = tempCell ^ newBit[7];
            //applying Sb1
            int firstHalf = (tempCell & 0xf0) >> 4;
            int secondHalf = (tempCell & 0x0f);
            firstHalf = Sb1[firstHalf];
            secondHalf = Sb1[secondHalf];
            tempCell = 0;
            tempCell = tempCell ^ (firstHalf << 4);
            tempCell = tempCell ^ secondHalf;
            //converting two hexes to array of 8 bits
            mask = 0x80;
            Array.Clear(bit, 0, 8);
            Array.Clear(newBit, 0, 8);
            for (int i = 0; i < 8; i++)
            {
                bit[i] = tempCell & mask;
                bit[i] = bit[i] >> (7 - i);
                mask = mask >> 1;
            }
            //applying reverse pemutation
            for (int i = 0; i < 8; i++)
            {
                newBit[i] = bit[newIdx[i]];
            }
            //assemble bits to hexes
            for (int i = 0; i < 7; i++)
            {
                resCell = resCell ^ newBit[i];
                resCell = resCell << 1;
            }
            resCell = resCell ^ newBit[7];
            return resCell;
        }

        static int SSb1(int cell)
        {
            int[] newIdx = new int[8] { 1, 6, 7, 0, 5, 2, 3, 4 };
            int[] newIdxRev = new int[8] { 3, 0, 5, 6, 7, 4, 1, 2 };
            int[] bit = new int[8];      
            int[] newBit = new int[8];
            int tempCell = 0;
            int resCell = 0;
            int mask = 0x80;
            //converting two hexes to array of 8 bits
            for (int i = 0; i < 8; i++)
            {
                bit[i] = cell & mask;
                bit[i] = bit[i] >> (7 - i);
                mask = mask >> 1;
            }
            //applying pemutation
            for (int i = 0; i < 8; i++)
            {
                newBit[i] = bit[newIdx[i]];
            }
            //assemble bits to hexes
            for (int i = 0; i < 7; i++)
            {
                tempCell = tempCell ^ newBit[i];
                tempCell = tempCell << 1;
            }
            tempCell = tempCell ^ newBit[7];
            //applying Sb1
            int firstHalf = (tempCell & 0xf0) >> 4;
            int secondHalf = (tempCell & 0x0f);
            firstHalf = Sb1[firstHalf];
            secondHalf = Sb1[secondHalf];
            tempCell = 0;
            tempCell = tempCell ^ (firstHalf << 4);
            tempCell = tempCell ^ secondHalf;
            //converting two hexes to array of 8 bits
            mask = 0x80;
            Array.Clear(bit, 0, 8);
            Array.Clear(newBit, 0, 8);
            for (int i = 0; i < 8; i++)
            {
                bit[i] = tempCell & mask;
                bit[i] = bit[i] >> (7 - i);
                mask = mask >> 1;
            }
            //applying reverse pemutation
            for (int i = 0; i < 8; i++)
            {
                newBit[i] = bit[newIdxRev[i]];
            }
            //assemble bits to hexes
            for (int i = 0; i < 7; i++)
            {
                resCell = resCell ^ newBit[i];
                resCell = resCell << 1;
            }
            resCell = resCell ^ newBit[7];
            return resCell;
        }

        static int SSb2(int cell)
        {   
            int[] newIdx = new int[8] { 2, 3, 4, 1, 6, 7, 0, 5 };
            int[] newIdxRev = new int[8] { 6, 3, 0, 1, 2, 7, 4, 5 };
            int[] bit = new int[8];
            int[] newBit = new int[8];
            int tempCell = 0;
            int resCell = 0;
            int mask = 0x80;
            //converting two hexes to array of 8 bits
            for (int i = 0; i < 8; i++)
            {
                bit[i] = cell & mask;
                bit[i] = bit[i] >> (7 - i);
                mask = mask >> 1;
            }
            //applying pemutation
            for (int i = 0; i < 8; i++)
            {
                newBit[i] = bit[newIdx[i]];
            }
            //assemble bits to hexes
            for (int i = 0; i < 7; i++)
            {
                tempCell = tempCell ^ newBit[i];
                tempCell = tempCell << 1;
            }
            tempCell = tempCell ^ newBit[7];
            //applying Sb1
            int firstHalf = (tempCell & 0xf0) >> 4;
            int secondHalf = (tempCell & 0x0f);
            firstHalf = Sb1[firstHalf];
            secondHalf = Sb1[secondHalf];
            tempCell = 0;
            tempCell = tempCell ^ (firstHalf << 4);
            tempCell = tempCell ^ secondHalf;
            //converting two hexes to array of 8 bits
            mask = 0x80;
            Array.Clear(bit, 0, 8);
            Array.Clear(newBit, 0, 8);
            for (int i = 0; i < 8; i++)
            {
                bit[i] = tempCell & mask;
                bit[i] = bit[i] >> (7 - i);
                mask = mask >> 1;
            }
            //applying reverse pemutation
            for (int i = 0; i < 8; i++)
            {
                newBit[i] = bit[newIdxRev[i]];
            }
            //assemble bits to hexes
            for (int i = 0; i < 7; i++)
            {
                resCell = resCell ^ newBit[i];
                resCell = resCell << 1;
            }
            resCell = resCell ^ newBit[7];
            return resCell;
        }

        static int SSb3(int cell)
        {                             
            int[] newIdx = new int[8] { 7, 4, 1, 2, 3, 0, 5, 6 };
            int[] newIdxRev = new int[8] { 5, 2, 3, 4, 1, 6, 7, 0 };
            int[] bit = new int[8];
            int[] newBit = new int[8];
            int tempCell = 0;
            int resCell = 0;
            int mask = 0x80;
            //converting two hexes to array of 8 bits
            for (int i = 0; i < 8; i++)
            {
                bit[i] = cell & mask;
                bit[i] = bit[i] >> (7 - i);
                mask = mask >> 1;
            }
            //applying pemutation
            for (int i = 0; i < 8; i++)
            {
                newBit[i] = bit[newIdx[i]];
            }
            //assemble bits to hexes
            for (int i = 0; i < 7; i++)
            {
                tempCell = tempCell ^ newBit[i];
                tempCell = tempCell << 1;
            }
            tempCell = tempCell ^ newBit[7];
            //applying Sb1
            int firstHalf = (tempCell & 0xf0) >> 4;
            int secondHalf = (tempCell & 0x0f);
            firstHalf = Sb1[firstHalf];
            secondHalf = Sb1[secondHalf];
            tempCell = 0;
            tempCell = tempCell ^ (firstHalf << 4);
            tempCell = tempCell ^ secondHalf;
            //converting two hexes to array of 8 bits
            mask = 0x80;
            Array.Clear(bit, 0, 8);
            Array.Clear(newBit, 0, 8);
            for (int i = 0; i < 8; i++)
            {
                bit[i] = tempCell & mask;
                bit[i] = bit[i] >> (7 - i);
                mask = mask >> 1;
            }
            //applying reverse pemutation
            for (int i = 0; i < 8; i++)
            {
                newBit[i] = bit[newIdxRev[i]];
            }
            //assemble bits to hexes
            for (int i = 0; i < 7; i++)
            {
                resCell = resCell ^ newBit[i];
                resCell = resCell << 1;
            }
            resCell = resCell ^ newBit[7];
            return resCell;
        }

        static int[] SubCell128(int[] State)
        {
            int[] NewState = new int[16];
            for (int i = 0; i < 4; i++)
            {
                NewState[4 * i] = SSb0(State[4 * i]);
                NewState[4 * i + 1] = SSb1(State[4 * i + 1]);
                NewState[4 * i + 2] = SSb2(State[4 * i + 2]);
                NewState[4 * i + 3] = SSb3(State[4 * i + 3]);
            }

            return NewState;
        }

        static int[] ShuffleCell(int[] State)
        {
            int[] NewIdxs = new int[16] { 0, 10, 5, 15, 14, 4, 11, 1, 9, 3, 12, 6, 7, 13, 2, 8 };
            int[] NewState = new int[16];
            for (int i = 0; i < 16; i++)
            {
                NewState[i] = State[NewIdxs[i]];
            }
            return NewState;
        }

        static int[] MixColumns(int[] State)
        {
            int[] cell = new int[State.Length];
            for (int i = 0; i < 16; i++)
            { 
                cell[i] = State[i];
            }
            for (int i = 0; i < 4; i++)
            {
                State[i * 4] = cell[4 * i + 1] ^ cell[4 * i + 2] ^ cell[4 * i + 3];
                State[i * 4 + 1] = cell[4 * i] ^ cell[4 * i + 2] ^ cell[4 * i + 3];
                State[i * 4 + 2] = cell[4 * i] ^ cell[4 * i + 1] ^ cell[4 * i + 3];
                State[i * 4 + 3] = cell[4 * i] ^ cell[4 * i + 1] ^ cell[4 * i + 2];
            }

            return State;
        }



        static string Midori64Core(string plainText, int[] WK, int[] K0, int[] K1)
        {
            int[] S = new int[16];
            for (int i = 0; i < 16; i++)
            {
                S[i] = Convert.ToInt32("0x" + plainText[i], 16);
            }
            S = KeyAdd(S, WK, -1);
            for (int i = 0; i <= 14; i++)
            {
                S = SubCell64(S);
                S = ShuffleCell(S);
                S = MixColumns(S);
                if (i % 2 == 0)
                    S = KeyAdd(S, K0, i);
                else
                    S = KeyAdd(S, K1, i);
            }
            S = SubCell64(S);
            var res = KeyAdd(S, WK, -1);
            string Y = "";
            for (int i = 0; i < 16; i++)
            {
                Y = Y +Convert.ToString(res[i], 16);
            }
            return Y;   
        }

        static string Midori128Core(string plainText, int[] WK)
        {
            int[] S = new int[16];
            for (int i = 0; i < 16; i++)
            {
                S[i] = Convert.ToInt32("0x" + plainText.Substring(2 * i, 2), 16);
            }
            S = KeyAdd(S, WK, -1);
            for (int i = 0; i <= 18; i++)
            {
                S = SubCell128(S);
                S = ShuffleCell(S);
                S = MixColumns(S);
                S = KeyAdd(S, WK, i);
            }
            S = SubCell128(S);
            var res = KeyAdd(S, WK, -1);
            string Y = "";
            for (int i = 0; i < 16; i++)
            {
                Y = Y + Convert.ToString( (res[i] & 0xf0) >> 4, 16);
                Y = Y + Convert.ToString((res[i] & 0x0f), 16);
            }
            return Y;
        }



        //////////// Truncated Differetials ////////////

        static List<int> findSubArray4Bit(int[] array)
        {
            List<int> subArray = new List<int>();
            int mask;
            for (int i = 0; i <= 0xf; i++) // filling list with all possiible vlues
                subArray.Add(i);
            for(int i = 0; i < 4; i++) // removing all instances that don't match to the chosen bit
            {
                if (array[i] == 0 || array[i] == 1)
                {
                    mask = 1 << (3 - i);
                    subArray.RemoveAll(item => ((item & mask) >> (3 - i)) != array[i]);
                    
                }
            }

            return subArray;
        }

        /*
        static List<int> findSubArray8Bit(int[] array)
        {
            List<int> subArray = new List<int>();
            int mask;
            for (int i = 0; i <= 0xff; i++) // filling list with all possiible vlues
                subArray.Add(i);
            for (int i = 0; i < 8; i++) // removing all instances that don't match to the chosen bit
            {
                if (array[i] == 0 || array[i] == 1)
                {
                    mask = 1 << (7 - i);
                    subArray.RemoveAll(item => ((item & mask) >> (7 - i)) != array[i]);

                }
            }

            return subArray;
        }
        */

        static double TDPforSb0(int[] alpha, int[] beta)
        {
            double res;
            int counterX = 0, counterA;
            List<int> subAlpha = findSubArray4Bit(alpha);  // possible alpha & beta
            List<int> subBeta = findSubArray4Bit(beta);
            
            for (int x = 0; x <= 0xf; x++)
            {
                counterA = 0;
                foreach (int a in subAlpha)
                {
                    if (subBeta.Exists(b => Sb0[x ^ a] == (Sb0[x] ^ b) ))
                        counterA++;                        
                }
                if (counterA == subAlpha.Count)
                    counterX++;
            }
            res = (double)counterX / 16;
            return res;
        }

        static double TDPforSb1(int[] alpha, int[] beta)
        {
            double res;
            int counterX = 0, counterA;
            List<int> subAlpha = findSubArray4Bit(alpha);  // possible alpha & beta
            List<int> subBeta = findSubArray4Bit(beta);

            for (int x = 0; x <= 0xf; x++)
            {
                counterA = 0;
                foreach (int a in subAlpha)
                {
                    if (subBeta.Exists(b => Sb1[x ^ a] == (Sb1[x] ^ b)))
                        counterA++;
                }
                if (counterA == subAlpha.Count)
                    counterX++;
            }
            res = (double)counterX / 16;
            return res;
        }

        static double TDPforSSb0(int[] alphaInput, int[] betaInput)
        {
            double res;
            int[] alphaTrue = new int[8];
            int[] betaTrue = new int[8];
            int[] newIdx = new int[8] { 4, 1, 6, 3, 0, 5, 2, 7 };
            int[] alphaFirst = new int[4];
            int[] alphaSecond = new int[4];
            int[] betaFirst = new int[4];
            int[] betaSecond = new int[4];

            for (int i = 0; i < 8; i++ ) // applying permutation
            {
                alphaTrue[i] = alphaInput[newIdx[i]];
                betaTrue[i] = betaInput[newIdx[i]];
            }
            for (int i = 0; i < 4; i++)
            {
                alphaFirst[i] = alphaTrue[i];
                alphaSecond[i] = alphaTrue[4 + i];
                betaFirst[i] = betaTrue[i];
                betaSecond[i] = betaTrue[4 + i];
            }
            res = TDPforSb1(alphaFirst,betaFirst) * TDPforSb1(alphaSecond,betaSecond);

            return res;
        }


        static int[] UpdateBeta64Bit(int[] beta)
        {
            int[] NewIdxs = new int[16] { 0, 10, 5, 15, 14, 4, 11, 1, 9, 3, 12, 6, 7, 13, 2, 8 };
            int[] betaTemp = new int[64];
            int[] betaUpdated = new int[64];
            for (int i = 0; i < 16; i++) // using ShuffleCell on beta's bits
            {
                betaTemp[i * 4] = beta[NewIdxs[i] * 4];
                betaTemp[i * 4 + 1] = beta[NewIdxs[i] * 4 + 1];
                betaTemp[i * 4 + 2] = beta[NewIdxs[i] * 4 + 2];
                betaTemp[i * 4 + 3] = beta[NewIdxs[i] * 4 + 3];
            }

            for (int i = 0; i < 4; i++) // using MixColumns on beta's bits
            {
                for (int j = 0; j < 4; j++)
                {
                    betaUpdated[i * 16 + j] = betaTemp[16 * i + j + 4] + betaTemp[16 * i + j + 8] + betaTemp[16 * i + j + 12];
                    betaUpdated[i * 16 + j + 4] = betaTemp[16 * i + j] + betaTemp[16 * i + j + 8] + betaTemp[16 * i + j + 12];
                    betaUpdated[i * 16 + j + 8] = betaTemp[16 * i + j] + betaTemp[16 * i + j + 4] + betaTemp[16 * i + j + 12];
                    betaUpdated[i * 16 + j + 12] = betaTemp[16 * i + j] + betaTemp[16 * i + j + 4] + betaTemp[16 * i + j + 8];
                }
            }

            for (int i = 0; i < betaUpdated.Length; i++)
            {
                if (betaUpdated[i] > 3)
                    betaUpdated[i] = 4;
                else
                    betaUpdated[i] = betaUpdated[i] & 0x1;
            }

            return betaUpdated;

        }

        static double TDPforM64(int[] alphaInput, int[] betaInputIncorrect) // calculates TDP for Midori64 and finds beta's correct value
        {
            // alphaInput.length == betaInputIncorrect.length == 64
            double result = 1;
            int[] alphaCell = new int[4]; // midori64's cell
            int[] betaCell = new int[4];
            for (int i = 0; i < 16; i++ )
            {
                Array.Clear(alphaCell, 0, alphaCell.Length);
                Array.Clear(betaCell, 0, betaCell.Length);
                for (int j = 0; j < 4; j++) // copies the bits needed for i-th SBox 
                {
                    alphaCell[j] = alphaInput[i * 4 + j];
                    betaCell[j] = betaInputIncorrect[i * 4 + j];    
                }
                result = result * TDPforSb0(alphaCell, betaCell); 
            }
            
            return result;
        }

        static int[] ConvertToBits(string input) // returns an array of bits
        {
            int size = input.Length;
            int[] result  = new int[size];
            for (int i = 0; i < size; i++)
            {
                result[i] = Convert.ToInt32("0x" + input[i],16);
            }
            return result;
        }

        static void TDDT4Bits() // print TDDT
        {
            //int[] bits = new int[3] { 0, 1, 4 };
            string[] diffs = new string[65] {"0004",   "0040",   "0400",   "4000",  "0014",   "0041",   "0401",   "4001",
                                             "0104",   "0140",   "0410",   "4010",  "0114",   "0141",   "0411",   "4011",
                "1004",   "1040",   "1400",   "4100", "1014",   "1041",   "1401",   "4101", "1104",   "1140",   "1410",   
                "4110", "1114",  "1141",   "1411",   "4111", "0044",   "0404",   "4004",   "0440",   "4040",   "4400",
                "0144",   "0414",   "4014",   "0441",   "4041",   "4401", "1044",   "1404",   "4104",   "1440",   "4140", "4410",
                "1144",   "1414",   "4114",   "1441",   "4141",   "4411", "0444",   "4044",   "4404",   "4440", "1444",   "4144",   
                "4414",   "4441", "4444"};
            int[] a = new int[4];
            int[] b = new int[4];

            using (StreamWriter sw = File.CreateText("Sb0.txt"))
            {
                for (int ai = 0; ai < diffs.Length; ai++)
                {
                    sw.Write(diffs[ai]);
                    for (int bi = 0; bi < diffs.Length; bi++)
                    {
                        sw.Write(String.Format("{0,10:D}", TDPforSb0(ConvertToBits(diffs[ai]), ConvertToBits(diffs[bi])).ToString())); sw.Write("  ");
                    }
                    sw.Write("\n");
                    Console.WriteLine(diffs[ai]);
                }
            }
        }


        /*
        static void TDDT8Bits()
        {
            int[] bits = new int[3] { 0, 1, 4 };
            List<string> diffs = new List<string>();
            string a = " ";
            //for (int i= 0;)

            if (diffs.Exists(x => x == a ) == false)
                diffs.Add (a);
        }
        */

        static int[] findTruncatedDiff (List<int> diffs)
        {
            int[][] bitDiffs = new int[diffs.Count][];
            int[] result = new int[4] { 0, 0, 0, 0 };
            for (int i = 0; i < diffs.Count; i++)
                bitDiffs[i] = new int[4] ; 
            for(int i = 0; i < diffs.Count; i++)
            {
                bitDiffs[i][0] = (diffs[i] & 0x8) >> 3;
                bitDiffs[i][1] = (diffs[i] & 0x4) >> 2;
                bitDiffs[i][2] = (diffs[i] & 0x2) >> 1;
                bitDiffs[i][3] = (diffs[i] & 0x1);
                //bitDiffs[i] = ConvertToBits(Convert.ToString(0x1, 2)); 
                result[0] = result[0] + bitDiffs[i][0];
                result[1] = result[1] + bitDiffs[i][1];
                result[2] = result[2] + bitDiffs[i][2];
                result[3] = result[3] + bitDiffs[i][3];
            }
            for(int i = 0; i < 4; i++)
            {
                if (result[i] != 0 && result[i] != diffs.Count)
                    result[i] = 4;
                else
                    result[i] = result[i] / diffs.Count;
            }

            return result;
        }

        static Dictionary<string, double>TDPforM16_alpha(string alphaInput)
        {
            Dictionary<string, double> result = new Dictionary<string,double>();
            int[] alpha = ConvertToBits(alphaInput);
            int[] betaTemp = new int[alpha.Length];
            int[] beta = new int[alpha.Length];
            List<int> alphaCellSubarray = new List<int>();
            List<int> betaCellSubarray = new List<int>();
            double probability = 0;
            int[] bits = new int[4];
            for(int cell = 0; cell < 4; cell++)
            {
                alphaCellSubarray.Clear();
                betaCellSubarray.Clear();
                for (int i = 0; i < 4; i++) // filling bits needed for i-th cell
                {
                    bits[i] = alpha[cell * 4 + i];
                }
                alphaCellSubarray = findSubArray4Bit(bits);
                foreach (int element in alphaCellSubarray) // finding all possible SubCell results
                {
                    betaCellSubarray.Add(Sb0[element]);
                }
                var betaCell = findTruncatedDiff(betaCellSubarray); // finding truncated betaCell
                for (int i = 0; i < 4; i++)                         // writing betaCell to betaTemp
                    betaTemp[cell * 4 + i] = betaCell[i];

                probability *= TDPforSb0(bits, betaCell); // updating TDP                
            }

            // застосувати МіксКолумс до бетаТемп



            return result;
        }











        // unknown bits denoted as 4 or more 


        static void Main(string[] args)
        {

            int[] a = { 4, 4, 4, 4 };//, 1, 0, 0, 0 };
            //int[] b = { 4, 0, 0, 4, 1, 0, 4, 4 };
            //TDDT4Bits();
            //var t = TDPforSSb0(a, b);
            //var y = findSubArray4Bit(a);
            var subarray = findSubArray4Bit(a);
            var res = findTruncatedDiff(subarray);
            
            
            

            Console.WriteLine("end");
            Console.ReadKey();
        }
    }
}

