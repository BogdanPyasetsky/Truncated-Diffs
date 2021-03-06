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
        ////////////////////////////////////////////////
        //////////// Midori64 and Midori128 ////////////
        ////////////////////////////////////////////////

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
            int[] Sum = new int[n];

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


        static int[] KeyAdd(int[] state, int[] key, int iteration)
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

        static int SSb0(int cell)
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
                Y = Y + Convert.ToString(res[i], 16);
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
                Y = Y + Convert.ToString((res[i] & 0xf0) >> 4, 16);
                Y = Y + Convert.ToString((res[i] & 0x0f), 16);
            }
            return Y;
        }


        ////////////////////////////////////////////////
        //////////// Truncated Differetials ////////////
        ////////////////////////////////////////////////


        private static string[] trunc4BitDiffs = new string[65] {"0004",   "0040",   "0400",   "4000",  "0014",   "0041",   "0401",   "4001",
                                             "0104",   "0140",   "0410",   "4010",  "0114",   "0141",   "0411",   "4011",
                "1004",   "1040",   "1400",   "4100", "1014",   "1041",   "1401",   "4101", "1104",   "1140",   "1410",
                "4110", "1114",  "1141",   "1411",   "4111", "0044",   "0404",   "4004",   "0440",   "4040",   "4400",
                "0144",   "0414",   "4014",   "0441",   "4041",   "4401", "1044",   "1404",   "4104",   "1440",   "4140", "4410",
                "1144",   "1414",   "4114",   "1441",   "4141",   "4411", "0444",   "4044",   "4404",   "4440", "1444",   "4144",
                "4414",   "4441", "4444"};
        private static string[] _4BitDiffs = new string[16] {
        "0000", "0001", "0010", "0011", "0100", "0101", "0110", "0111", "1000", "1001", "1010", "1011", "1100", "1101", "1110", "1111" };


        static List<int> findSubArray4Bit(int[] array)
        {
            List<int> subArray = new List<int>();
            int mask;
            for (int i = 0; i <= 0xf; i++) // filling list with all possiible vlues
                subArray.Add(i);
            for (int i = 0; i < 4; i++) // removing all instances that don't match to the chosen bit
            {
                if (array[i] == 0 || array[i] == 1)
                {
                    mask = 1 << (3 - i);
                    subArray.RemoveAll(item => ((item & mask) >> (3 - i)) != array[i]);

                }
            }

            return subArray;
        }

       
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
                    if (subBeta.Exists(b => Sb0[x ^ a] == (Sb0[x] ^ b)))
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

            for (int i = 0; i < 8; i++) // applying permutation
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
            res = TDPforSb1(alphaFirst, betaFirst) * TDPforSb1(alphaSecond, betaSecond);

            return res;
        }


        static int[] ConvertToBits(string input) // returns an array of bits
        {
            int size = input.Length;
            int[] result = new int[size];
            for (int i = 0; i < size; i++)
            {
                result[i] = Convert.ToInt32("0x" + input[i], 16);
            }
            return result;
        }

        static void TDDT4Bits() // print TDDT
        {
            List<string> diffs4Bit = _4BitDiffs.ToList(); // printing all diffs
            for (int i = 0; i < trunc4BitDiffs.Length; i++)
                diffs4Bit.Add(trunc4BitDiffs[i]);
            using (StreamWriter sw = File.CreateText("Sb1(1).txt"))
            {
                int n;
                double probability; 
                                
                foreach (var alpha in diffs4Bit)
                {

                    sw.WriteLine("-------------" + alpha + "-------------");
                    foreach (var beta in diffs4Bit)
                    {
                        probability = TDPforSb1(ConvertToBits(alpha), ConvertToBits(beta));
                        if (probability > 0)
                            sw.WriteLine(beta + "   " + probability);
                    }                   
                
                }
                
            }
        }

        static void TDDT8Bits() // print TDDT
        {
            List<string> diffs4Bit = _4BitDiffs.ToList(); // printing all diffs
            for (int i = 0; i < trunc4BitDiffs.Length; i++)
                diffs4Bit.Add(trunc4BitDiffs[i]);
            List<string> diffs8Bit = new List<string>();
            List<string> truncDiffs8Bit = new List<string>();
            double probability;

            foreach (var fHalf in diffs4Bit)
            {
                foreach (var sHalf in diffs4Bit)
                {
                    diffs8Bit.Add(fHalf + sHalf);
                }
            }

            
            foreach(var item in diffs8Bit)
            {
                if (item.Contains("4"))
                    truncDiffs8Bit.Add(item);
            }
            

            int t = 0;
            int counter = 0;
            using (StreamWriter sw = File.CreateText("SSb0(1).txt"))
            {
                foreach (var alpha in truncDiffs8Bit)
                {
                    sw.WriteLine("-------------" + alpha + "-------------");
                    foreach (var beta in truncDiffs8Bit)
                    {
                        probability = TDPforSSb0(ConvertToBits(alpha), ConvertToBits(beta));
                        if (probability > 0)
                        {
                            sw.WriteLine(beta + "   " + probability);
                            counter++;
                        }
                    }
                    Console.WriteLine(t + "  out of  " + truncDiffs8Bit.Count);
                    t++;
                }
            }

            Console.WriteLine("#non 0  " + counter);
            
        }


       

        static int[] findTruncatedDiff(List<int> diffs)
        {
            int[][] bitDiffs = new int[diffs.Count][];
            int[] result = new int[4] { 0, 0, 0, 0 };
            for (int i = 0; i < diffs.Count; i++)
                bitDiffs[i] = new int[4];
            for (int i = 0; i < diffs.Count; i++)
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
            for (int i = 0; i < 4; i++)
            {
                if (result[i] != 0 && result[i] != diffs.Count)
                    result[i] = 4;
                else
                    result[i] = result[i] / diffs.Count;
            }

            return result;
        }



        static Dictionary<string, double> TDPforM16_alpha(string alphaInput) // alphaInput string of 16 bits
        {
            Dictionary<string, double> result = new Dictionary<string, double>();
            List<string> diffs = trunc4BitDiffs.ToList(); // printing all 4 bit diffs
            for (int i = 0; i < 16; i++)
                diffs.Add(_4BitDiffs[i]);
            Dictionary<string, double>[] betaCell =
            {
                new Dictionary<string, double>(),
                new Dictionary<string, double>(),
                new Dictionary<string, double>(),
                new Dictionary<string, double>(),
            };
            int[] alpha = ConvertToBits(alphaInput);
            int[] bits = new int[4];
            double cellProbability;
            
            // finding all possible betas for each alphaCell
            for (int cell = 0; cell < 4; cell++)
            {
                for (int i = 0; i < 4; i++) // filling bits needed for i-th cell
                {
                    bits[i] = alpha[cell * 4 + i];
                }
                foreach (string beta in diffs)
                {
                    cellProbability = TDPforSb0(bits, ConvertToBits(beta));
                    if (cellProbability > 0) // initial probability filtering
                        betaCell[cell].Add(beta, cellProbability);
                }
            }
            // combinning all betaCells together
            Dictionary<string, double> betaTemp = new Dictionary<string, double>();
            foreach(var pair0 in betaCell[0])
            {
                foreach (var pair1 in betaCell[1])
                {
                    foreach (var pair2 in betaCell[2])
                    {
                        foreach (var pair3 in betaCell[3])
                        {
                            betaTemp.Add(pair0.Key + pair1.Key + pair2.Key + pair3.Key, pair0.Value * pair1.Value * pair2.Value * pair3.Value);
                        }
                    }
                }
                
            }
           

            int[] betaTempBits = new int[16];
            int[] betaBits = new int[16];
            string betaString;

            foreach (var pair in betaTemp)
            {
                betaString = "";
                betaTempBits = ConvertToBits(pair.Key);
                for (int i = 0; i < 4; i++) // applying MixColumn
                {
                    betaBits[i] = betaTempBits[i + 4] + betaTempBits[i + 8] + betaTempBits[i + 12];
                    betaBits[i + 4] = betaTempBits[i] + betaTempBits[i + 8] + betaTempBits[i + 12];
                    betaBits[i + 8] = betaTempBits[i] + betaTempBits[i + 4] + betaTempBits[i + 12];
                    betaBits[i + 12] = betaTempBits[i] + betaTempBits[i + 4] + betaTempBits[i + 8];
                }
                for (int i = 0; i < betaBits.Length; i++)
                {
                    if (betaBits[i] > 3)
                        betaBits[i] = 4;
                    else
                        betaBits[i] = betaBits[i] & 0x1;
                    betaString += Convert.ToString(betaBits[i]);
                }
                if (result.ContainsKey(betaString) == true)
                    result[betaString] = Math.Max(pair.Value, result[betaString]);
                else
                    result.Add(betaString, pair.Value);
            }

            return result;           
        }


        static Dictionary<string, double> DifferentialSearch(string alphaInput, int depth, double[] limits)
        {
            Dictionary<string, double>[] gamma =
            {
                new Dictionary<string, double>(),
                new Dictionary<string, double>(),
                new Dictionary<string, double>(),
                new Dictionary<string, double>(),
                new Dictionary<string, double>(),
                new Dictionary<string, double>(),
                new Dictionary<string, double>(),
                new Dictionary<string, double>(),
                new Dictionary<string, double>(),
                new Dictionary<string, double>(),
            };
            Dictionary<string, double> gammaTemp = new Dictionary<string, double>();

            List<string> keysPrev = new List<string>();
            List<string> keys = new List<string>();
            double probabilityTemp;

            gamma[0] = TDPforM16_alpha(alphaInput); // filling the first layer
            keys = gamma[0].Keys.ToList();
            foreach (string key in keys)
            {
                if (gamma[0][key] <= limits[0])
                    gamma[0].Remove(key);
            }


            // filling remaining layers
            for (int t = 1; t < depth; t++)
            {
                keysPrev.Clear(); keysPrev = gamma[t - 1].Keys.ToList(); // savin all betas from previous layer

                foreach (var KeyPrev in keysPrev)
                {
                    gammaTemp.Clear();
                    gammaTemp = TDPforM16_alpha(KeyPrev); // for each key finding all possible routes

                    foreach (var pair in gammaTemp) // updating dictionary gamma[t] keys and values
                    {
                        probabilityTemp = gamma[t - 1][KeyPrev] * pair.Value;
                        if (gamma[t].ContainsKey(pair.Key))
                            gamma[t][pair.Key] = Math.Max(probabilityTemp, pair.Value);
                        else
                            gamma[t].Add(pair.Key, probabilityTemp);
                    }
                }
                keys.Clear(); keys = gamma[t].Keys.ToList();
                foreach (string key in keys)
                {
                    if (gamma[t][key] <= limits[t])
                        gamma[t].Remove(key);
                }

            }
                return gamma[depth - 1];            
        }






        // unknown bits are denoted as 4 or more 
        static void Main(string[] args)
        {

            //string input = "1000000000000004";
            //double[] limits = { 0.002, 0.002, 0.0001, 0.0001, 0.0001, 0.0001, 0.0001, 0.0001, 0.0001, 0.0001 };


            string input = "0000000000000001";
            double[] limits = { 0.2, 0.04, 0.002, 0.00001, 0.00003, 0.0001, 0.0001, 0.0001, 0.0001, 0.0001 };


            var dsr = DifferentialSearch(input, 3, limits);

            using (StreamWriter sw = File.CreateText("input_0000000000000001_d3.txt"))
            {
                foreach (var pair in dsr)
                {
                    sw.WriteLine(pair.Key + "   " + pair.Value);
                }

            }
                                   
        }
    }
}

