﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace dumplib.Text
{
    public static class Transcode
    {
        private class MatchFrame
        {
            public MatchFrame(LogicalTable Table, int MatchCount)
            {
                this.Table = Table;
                this.MatchCount = MatchCount;
                this.OrigSeq = null;
            }

            public LogicalTable Table
            {
                get;
                private set;
            }

            public int MatchCount
            {
                get;
                private set;
            }

            public byte[] OrigSeq
            {
                get;
                set;
            }

            public byte MatchByte
            {
                get;
                set;
            }

            public void Decrease()
            {
                if (this.MatchCount > 1) MatchCount--;
            }
        }

        /// <summary>
        /// Decodes text data using a text table
        /// </summary>
        /// <param name="Data">Array of bytes to decode</param>
        /// <param name="Table">Text table</param>
        /// <param name="StartTable">Name of the logical table in the text table to start with</param>
        /// <returns></returns>
        public static string UsingTable(byte[] Data, Table Table, string StartTable = "{main}", bool ShowAddr = false, uint StartOffset = 0)
        {
            
            var _out = new StringBuilder();
            byte[] testseq;
            
            var TableStack = new Stack<MatchFrame>();     // tracks which tables have been switched from
            MatchFrame thisframe = new MatchFrame(Table.LogicalTables[StartTable],0);
            bool matched;

            int testloop; // loop pointer for test sequence
            int remaining = 0;  // bytes remaining in _in array

            // begin loop for all bytes in array passed to this method
            for (int outerloop = 0; outerloop < Data.Length; )
            {
                // reset the match flag
                matched = false;
                // remaining bytes in the data buffer
                remaining = Data.Length - outerloop;

                /*      Search Loop
                 * 1. Make a byte array beginning with the current byte from the for loop
                 * 2. Add x more elements of bytes ahead of current position, where x is the Byte Width (max number of hex digits in logical table)
                 * 3. Check if this byte sequence is in the control code/table switch/end token lists for this logical table
                 * 4. If not, check if this byte sequence is in the dictionary
                 * 5. If not, go back to 1 with using Byte Width - 1
                 * 6. When back down to the original single byte, if it is still not found in the lists of the dict, it is not in the table
                 */
                
                // increase testloop by the ByteWidth
                // ensure that the length of the testseq does not go past the upper bound of _in
                testloop = thisframe.Table.ByteWidth > remaining ? remaining : thisframe.Table.ByteWidth;

                // Loop for test byte sequence
                for (; testloop > 0; testloop--)
                {
                    // make the testseq as long as testloop and copy that many bytes from _in @ current position (i)
                    testseq = new byte[testloop];
                    Buffer.BlockCopy(Data, outerloop, testseq, 0, testloop);

                    // check to see if this test sequence matches the previous table switch sequence, if the match mode is -1
                    if (thisframe.MatchCount == -1 && thisframe.OrigSeq.SequenceEqual<byte>(testseq))
                    {
                        // then fall back to the previous logical table (match frame)
                        thisframe = TableStack.Pop();
                        outerloop++;
                        break;
                    }

                    // check each of the dictionaries for the test bytes
                    // table switch
                    if (thisframe.Table.TableSwitches.ContainsKey(testseq))
                    {
                        // cache the variable for speed
                        var thisswitch = thisframe.Table.TableSwitches[testseq];
                        TableStack.Push(thisframe);
                        thisframe = new MatchFrame(Table.LogicalTables[thisswitch.TableID], thisswitch.Matches);
                        if (thisframe.MatchCount == -1) thisframe.OrigSeq = testseq;
                        if (thisframe.MatchCount > 0) thisframe.MatchByte = testseq[0];
                        outerloop += testloop;
                        break;
                    }

                    // control code
                    if (thisframe.Table.ControlCodes.ContainsKey(testseq))
                    {
                        // NOTE! It is calling the byte comparer EVERY TIME ControlCodes[testseq] is caleld. cache the variable??
                        // cached variable for speed
                        var thiscode = thisframe.Table.ControlCodes[testseq];
                        if (thiscode.Params == null)
                        {
                            // this control code has no paramaters, so use quick and easy formatting
                            matched = true;
                            _out.Append('[' + thiscode.Label + ']');
                            _out.Append(thiscode.Formatting);
                            outerloop += testloop;
                            break;
                        }
                        else
                        {
                            matched = true;
                            int paramoffset = outerloop + testloop;
                            int paramcount = 0;
                            _out.Append('[' + thiscode.Label);
                            foreach (LogicalTable.ControlCode.Parameter thiscodeparam in thiscode.Params)
                            {
                                switch (thiscodeparam.Type)
                                {
                                    case LogicalTable.ControlCode.Parameter.NumberType.Hex:
                                        _out.Append(' ' + thiscodeparam.Label + "=0x" + Data[paramoffset + paramcount].ToString("X"));
                                        break;
                                    case LogicalTable.ControlCode.Parameter.NumberType.Decimal:
                                        _out.Append(' ' + thiscodeparam.Label + '=' + Data[paramoffset + paramcount].ToString());
                                        break;
                                    case LogicalTable.ControlCode.Parameter.NumberType.Binary:
                                        _out.Append(' ' + thiscodeparam.Label + '=' + Convert.ToString(Data[paramoffset + paramcount], 2));
                                        break;
                                }
                                paramcount++;
                            }

                            _out.Append(']');
                            _out.Append(thiscode.Formatting);
                            outerloop += (testloop + paramcount);
                            break;
                        }
                    }

                    // end token
                    if (thisframe.Table.EndTokens.ContainsKey(testseq))
                    {
                        // cached variable
                        var thistoken = thisframe.Table.EndTokens[testseq];
                        matched = true;
                        _out.Append("[" + thistoken.Label + "]");
                        _out.Append(thistoken.Formatting);
                        if (ShowAddr)
                            _out.AppendLine("[0x" + (StartOffset + outerloop + 1).ToString("X") + "]");
                        outerloop += testloop;
                        break;
                    }
                    
                    // not found among any of the non-standard entries so check standard
                    if (thisframe.Table.StdDict.ContainsKey(testseq))
                    {
                        matched = true;
                        _out.Append(thisframe.Table.StdDict[testseq]);
                        outerloop += testloop;
                        break;
                    }

                    // if we are down to the last iteration (original byte) and still haven't found anything, the byte isn't found
                    if (testloop == 1)
                    {
                        matched = false;
                        if (TableStack.Count > 0 && thisframe.MatchCount == 0) thisframe = TableStack.Pop();
                        else
                        {
                            _out.Append("[" + testseq[0].ToString("X2") + "]");
                            outerloop++;
                        }
                    }
                }
                // end of test loop
                // 
                // if there are match frames on the table stack, let's check for a match and act accordingly
                if (TableStack.Count > 0)
                {
                    // matching mode is positive value [with value at 1, the minimum] and there was a match
                    // then return to the previous logical table (matchframe)
                    if (thisframe.MatchCount == 1 && matched)
                        thisframe = TableStack.Pop();
                    // else if matching mode is positive value [with value greater than 1] and there was a match
                    // then decrease the value by 1
                    else if (thisframe.MatchCount > 1 && matched) thisframe.Decrease();
                }
                // didn't find nuthin'!
                
            }
            return _out.ToString();
        }

        /*
        private static readonly Dictionary<byte, string> ASCIICodes = new Dictionary<byte, string>() { 
            {0,"[NUL]"},
            {1,"[SOH]"},
            {2,"[STX]"},
            {3,"[ETX]"},
            {4,"[EOT]"},
            {5,"[ENQ]"},
            {6,"[ACK]"},
            {7,"[BEL]"},
            {8,"[BS]"},
            {9,"[TAB]"},
            {10,"[LF]"},
            {11,"[VT]"},
            {12,"[FF]"},
            {13,"[CR]"},
            {14,"[SO]"},
            {15,"[SI]"},
            {16,"[DLE]"},
            {17,"[DC1]"},
            {18,"[DC2]"},
            {19,"[DC3]"},
            {20,"[DC4]"},
            {21,"[NAK]"},
            {22,"[SYN]"},
            {23,"[ETB]"},
            {24,"[CAN]"},
            {25,"[EM]"},
            {26,"[SUB]"},
            {27,"[ESC]"},
            {28,"[FS]"},
            {29,"[GS]"},
            {30,"[RS]"},
            {31,"[US]"}
        };
        */

        public static string UsingASCII(byte[] Data)
        {
            //return UsingCodePage(Data, 20127);
            return UsingEncoding(Data, Encoding.ASCII);
        }

        public static string UsingSJIS(byte[] Data)
        {
            return UsingEncoding(Data, 932);
        }

        public static string UsingEncoding(byte[] Data, int CodePage)
        {
            return UsingEncoding(Data, Encoding.GetEncoding(CodePage));
        }

        /// <summary>
        /// Decodes text using the specified encoding and substitutes null characters with text
        /// </summary>
        /// <param name="Data"></param>
        /// <param name="Encoding"></param>
        /// <returns></returns>
        public static string UsingEncoding(byte[] Data, Encoding Encoding)
        {
            if (Data == null) throw new ArgumentNullException();

            // Can't use this method because it chokes when it hits a 0!
            //return Encoding.GetString(Data, 0, Data.Length);
            
            StringBuilder _out = new StringBuilder();
            int offset = 0;
            int length = 0;
            byte[] tempdata;

            byte _work;

            //NEW VERSION 2
            // cycle through the array, find all 0's change them to 0x20 (space)
            // kinda meh but w/e
            for (int fixloop = 0; fixloop < Data.Length; fixloop++)
            {
                if (Data[fixloop] == 0) Data[fixloop] = 0x20;
            }

            return Encoding.GetString(Data, 0, Data.Length);

            //for (int workloop = 0; workloop < Data.Length; workloop++)
            //{
                
                

                // NEW VERSION 1
                // cycle through each byte until a 0 is encountered
                // submit that section of bytes to the encoder
                // turn the 0 into a space and move on
                /*_work = Data[workloop];
                //if (_work == 0) _out.Append("[NUL]");
                if (_work == 0)
                {
                    if (length > 0)
                    {
                        tempdata = new byte[length];
                        Buffer.BlockCopy(Data, offset, tempdata, 0, length);
                        var t = Encoding.GetString(tempdata, 0, length);
                        _out.Append(t);
                    }
                    _out.Append(' ');
                    offset = workloop + 1;
                    length = 0;
                }
                else
                {
                    //_out.Append(Encoding.GetString(Data, workloop, 1));
                    length++;
                }*/

                // OLD KLUDGY VERSION
                /*
                _work = Data[workloop];
                if (_work == 0) _out.Append(" ");
                else
                {
                  _out.Append(Encoding.GetString(Data, workloop, 1));
                }*/
            //}
            // part of new version 1:
            /*if (length > 0)
            {
                tempdata = new byte[length];
                Buffer.BlockCopy(Data, offset, tempdata, 0, length);
                _out.Append(Encoding.GetString(tempdata, 0, length));
            }*/
            
            //return _out.ToString();
            
        }
    }
}
