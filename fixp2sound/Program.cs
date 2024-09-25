using System.Drawing;
using xayrga;
using xayrga.byteglider;
using xayrga.console;

namespace fixp2sound
{
    internal class Program
    {
        static bgReader Reader;
        static bgWriter Writer;

        private const int IBNK = 0x49424e4b;
        private const int BANK = 0x42414E4B;
        private const int INST = 0x494E5354;
        private const int PER2 = 0x50455232;
        private const int BANK_INST_COUNT = 0xF0;
       

        static void Main(string[] args)
        {
 
            ConsoleAppHelper.ArgumentList = args;
            var aafFile = ConsoleAppHelper.assertArg(0, "AAF File path");
            var confirm = !ConsoleAppHelper.findDynamicFlagArgument("no-confirm");
            ConsoleAppHelper.assert(File.Exists(aafFile), $"'{aafFile}' does not exist or cannot be accessed.");

            var fHnd = File.Open(aafFile, FileMode.Open, FileAccess.ReadWrite); //new MemoryStream(File.ReadAllBytes(aafFile));

            Reader = new bgReader(fHnd);
            Writer = new bgWriter(fHnd);

            if (!ConsoleAppHelper.findDynamicFlagArgument("skip-aaf-check"))            
                ConsoleAppHelper.assert(Reader.ReadUInt64() == 0xE800000001, "Invalid AAF, must be unmodified pikmin 2 AAF, or you can use argument '-skip-aaf-check' to ignore this and try it anyways.");

            Reader.Seek(0);

            Console.WriteLine($"Reading {aafFile}");
            var ibnkOffsets = getIBNKOffsets(Reader);
            Console.WriteLine($"{ibnkOffsets.Count} IBNK found");
            Console.WriteLine($"Loading SENS objects...");
            var allSensAddrs = new List<int>();
            for (int i = 0; i < ibnkOffsets.Count; i++)
            {
                var ofs = ibnkOffsets[i];
                Reader.SetBase(ofs);
                var addrs = loadIBNKSensAddrs(Reader);
                allSensAddrs.AddRange(addrs);
            }
            Reader.ClearBase();

            Console.WriteLine($"{allSensAddrs.Count} SENS objects.");
         

            List<int> badSENSAddr = new List<int>();
            for (int i = 0;i < allSensAddrs.Count;i++)
            {
                var currentAddr = allSensAddrs[i];
                Reader.Seek(currentAddr);
                var bytes = Reader.ReadBytes(16);
                Reader.Seek(currentAddr + 4);
                // First four bytes are usually fine.
                var bad = false; 

                // nab floating point representation
                var floor = Reader.ReadSingle();
                var ceil = Reader.ReadSingle();
                Reader.Seek(currentAddr + 4);
                // integer represenstaiton
                var floorI = Reader.ReadUInt32();
                var ceilI = Reader.ReadUInt32();

                if (isBadFloat(floor,floorI) || isBadFloat(ceil,ceilI) )
                    bad = true;

                var w = Console.ForegroundColor;
                Console.Write($"{currentAddr:X4}: ");
                if (bad)
                    Console.ForegroundColor = ConsoleColor.Red;

                for (int j = 0; j < bytes.Length; j++) 
                    Console.Write($"{bytes[j]:X2} ");
               
                Console.WriteLine($"| {floor}, {ceil}");
                Console.ForegroundColor = w;

                if (bad)
                    badSENSAddr.Add(currentAddr);
            }

            ConsoleAppHelper.assert(badSENSAddr.Count != 0, "Nothing to patch.");

            while (confirm)
            {
                Console.Write("I will correct the items above, this will make permanent changes to your AAF.\nPress Y to continue, N to cancel.: ");
                var key = Console.ReadKey();
                if (key.Key == ConsoleKey.Y)
                    break;
                else if (key.Key == ConsoleKey.N)
                    Environment.Exit(0);
            
                Console.WriteLine();
                Console.WriteLine("Invalid choice. Let me ask again:");
            }
            Console.WriteLine();
            Console.WriteLine("Patching ibnks...");
            for (int i=0; i < badSENSAddr.Count; i++)
            {
                var addr = badSENSAddr[i];
                Reader.Seek(addr + 4);
                var oldFloat1 = Reader.ReadSingleBE();
                var oldFloat2 = Reader.ReadSingleBE();
                Writer.Seek(addr + 4);
                Writer.Write(oldFloat1);
                Writer.Write(oldFloat2);
                Writer.Flush();
                Reader.Seek(addr);

                var bytes = Reader.ReadBytes(16);

                var w = Console.ForegroundColor;
                Console.Write($"{addr:X4}: ");     
                Console.ForegroundColor = ConsoleColor.Green;
                for (int j = 0; j < bytes.Length; j++)
                {
                    if (j >= 4 && j < 12)
                        Console.ForegroundColor = ConsoleColor.Green;
                    else
                        Console.ForegroundColor = w;
                  
                    Console.Write($"{bytes[j]:X2} ");
                }
                Console.WriteLine("|");

                Console.ForegroundColor = w;
            }
            Console.WriteLine("done.");
            Writer.Flush();
            Writer.Close();
        }

        static bool isBadFloat(float fRep, uint bytes)
        {
            // Checks if the exponent is really stupid when it's not supposed to be.
            var exponentF = (int)(((bytes >> 23)) & 0x7F) - 0x7F;
            if (exponentF == -127 && (fRep < 0.4) && fRep != 0)
                return true;
            return false;
        }

        static List<int> loadIBNKSensAddrs(bgReader reader)
        {
            var sensOffs = new Dictionary<int, int>();
            reader.Seek(0); // reset position
            if (reader.ReadUInt32() != IBNK)
                throw new Exception("Whoops. That's not an IBNK");
            reader.Seek(0x20);
            if (reader.ReadUInt32() != BANK)
                throw new Exception("Whoops. That IBNK is corrupted.");

            for (int i=0; i < BANK_INST_COUNT; i++)
            {
                var offset = reader.ReadUInt32();
                if (offset == 0)
                    continue;
                reader.PushAnchor();
                reader.Seek(offset);
                var type = reader.ReadUInt32();
                if (type == PER2) // nah
                    continue;
                else if (type != INST)
                    throw new Exception($"Whoops, unrecognized instrument type in this bank... {type:X} @ {reader.BaseStream.Position:X}");

                reader.Skip(0x1C); // skips all other components, oscillators, rand effects, pitch, volume, etc 
                
                var eff1 = reader.ReadInt32();
                var eff2 = reader.ReadInt32();


                var real_pos = reader.RelativeToAbsolutePosition(eff1);
                if (eff1 > 0)
                    if (!sensOffs.ContainsKey(real_pos))
                        sensOffs.Add(real_pos, 1);
                    else
                        sensOffs[real_pos]++;


                real_pos = reader.RelativeToAbsolutePosition(eff2);
                if (eff2>0)
                    if (!sensOffs.ContainsKey(real_pos))
                        sensOffs.Add(real_pos, 1);
                    else
                        sensOffs[real_pos]++;

                reader.PopAnchor();
            }
            foreach (var key in sensOffs)
            {
                //Console.WriteLine($"SENS {key.Key:X} referenced {key.Value} times");
            }

            return sensOffs.Keys.ToList();
        }

        static List<int> getIBNKOffsets(bgReader reader)
        {
            var ibnkOffsets = new List<int>();
            var go = true;
            while(go)
            {
                var chunkID = reader.ReadUInt32();
                switch (chunkID)
                {
                    case 0:
                        go = false;
                        break;
                    case 1:
                    case 4:
                    case 5:
                    case 6:
                    case 7:
                        Reader.Skip(12);
                        break;
                    case 3: 
                        while (true)
                        {
                            var offset = reader.ReadInt32();
                            if (offset == 0) break;
                            reader.Skip(12);
                        }
                        break;
                    case 2:
                        while (true)
                        {
                            var offset = reader.ReadInt32();
                            if (offset == 0) break;
                            reader.Skip(12);
                            ibnkOffsets.Add(offset);
                        }
                        break;
                    default:
                        throw new Exception("oops");
                }
            }
            return ibnkOffsets;
        }
    }
}
