using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace ExtendedBAT{
    public class ExtendedBAT{
        private static string compilerDir = new FileInfo(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName).Directory.FullName;

        public static void Main(string[] args){
            if(args.Length == 0){
                Console.WriteLine("Usage: exbat <fileName>");
                Environment.Exit(1);
            }

            File.WriteAllText(args[0].Replace(".exb", ".bat"), Compile(File.ReadAllText(args[0])));
        }

        public static string Compile(string content){
            string randName = GetRandomName(20), randFuncArg = GetRandomName(20);

            content = "@echo off"+Environment.NewLine+"setlocal EnableDelayedExpansion"+Environment.NewLine+content;

            content = Regex.Replace(content, @"include\((.+)\)", new MatchEvaluator((m)=>{
                return File.ReadAllText(compilerDir+"\\lib\\"+m.Groups[1].ToString()+".exb")+Environment.NewLine;
            }));

            Dictionary<string, string> repl = new Dictionary<string, string>();
            content = Regex.Replace(content, @"define\((.+), (.+)\)", new MatchEvaluator((m)=>{
                repl.Add(m.Groups[1].ToString(), m.Groups[2].ToString());
                return "";
            }));
            foreach(KeyValuePair<string, string> kvp in repl){
                content = content.Replace(kvp.Key, kvp.Value);
            }

            string[] fc = content.Split(new string[]{Environment.NewLine}, StringSplitOptions.None);
            bool isFunc = false, parse = true;
            string fnn = "";
            List<string> fna = new List<string>();
            int i = 0;

            foreach(string line in fc){
                if(line.TrimStart() == "purebatch"){
                    parse = true;
                }else if(line.TrimStart() == "end purebatch"){
                    parse = false;
                }

                if(!parse) continue;

                if(line.StartsWith("#") || line.StartsWith("//")){
                    fc[i] = line.Replace("#", "rem ").Replace("//", "rem ");
                }else if(line.TrimStart().StartsWith("noout(")){
                    fc[i] = line.Remove(line.Length-1).Replace("noout(", "")+"> nul";
                }else if(line.TrimStart().StartsWith("noerror(")){
                    fc[i] = line.Remove(line.Length-1).Replace("quiet(", "")+"2> nul";
                }else if(line.TrimStart().StartsWith("quiet(")){
                    fc[i] = line.Remove(line.Length-1).Replace("quiet(", "")+"> nul 2> nul";
                }else if(line.TrimStart().StartsWith("if(")){
                    string cond = Regex.Match(line, @"if\(([^\)]+)\)").Groups[1].ToString();
                    fc[i] = "if "+cond.Replace("<=", "leq").Replace(">=", "geq").Replace("<", "lss").Replace(">", "gtr").Replace(" == ", " equ ")+" (";
                }else if(line.TrimStart() == "end if"){
                    fc[i] = ")";
                }else if(line.TrimStart() == "else"){
                    fc[i] = ") else (";
                }else if(line.TrimStart().StartsWith("else if(")){
                    string cond = Regex.Match(line, @"else if\(([^\)]+)\)").Groups[1].ToString();
                    fc[i] = ") else if "+cond.Replace("<=", "leq").Replace(">=", "geq").Replace("<", "lss").Replace(">", "gtr").Replace(" == ", " equ ")+" (";
                }else if(line.TrimStart().StartsWith("function ")){
                    string[] spl = line.Replace("(", " ").Replace(")", "").Replace(", ", " ").Replace(",", " ").Split(' ');
                    isFunc = true;
                    fnn = spl[1];
                    foreach(string fa in spl.Skip(2)) fna.Add(fa);
                    fc[i] = ":"+fnn+Environment.NewLine+"if \"%~1\"==\""+randFuncArg+"\" ( shift /1 ) else ( goto _"+randName+"_end_"+fnn+" )";
                    fna.Add(fnn);
                }else if(line.TrimStart() == "end function"){
                    isFunc = false;
                    fna.Clear();
                    fc[i] = "goto :eof"+Environment.NewLine+":_"+randName+"_end_"+fnn;
                    fnn = "";
                }else if(isFunc){
                    for(int j = 0; j < fna.Count; j++){
                        fc[i] = fc[i].Replace("$"+fna[j], "%~"+(j+1).ToString());
                    }
                }else if(Regex.IsMatch(line, @"^[A-Za-z0-9_\-\.]+\([^\)]*\)$")){
                    var m = Regex.Match(line, @"^([A-Za-z0-9_\-\.]+)\(([^\)]*)\)$");
                    fc[i] = "call :"+m.Groups[1].ToString()+" "+randFuncArg+" "+m.Groups[2].ToString().Replace(", ", " ").Replace(",", " ").Replace(" out ", " ");
                }

                fc[i] = Regex.Replace(fc[i], @"\$([A-Za-z0-9_\-\.]+)", new MatchEvaluator((m)=>{
                    return "!"+m.Groups[1].ToString()+"!";
                }));

                i++;
            }

            return String.Join(Environment.NewLine, fc);
        }

        private static Random rand = new Random((int)DateTime.Now.Ticks);

        private static string GetRandomName(int len){
            string cs = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

            char[] chars = new char[len];
            for(int i = 0; i < len; i++){
                chars[i] = cs[rand.Next(cs.Length)];
            }

            return new String(chars);
        }
    }
}
