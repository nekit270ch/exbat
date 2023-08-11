using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace ExtendedBAT{
    public class ExtendedBAT{
        private static string compilerDir = new FileInfo(Process.GetCurrentProcess().MainModule.FileName).Directory.FullName;

        public static void Main(string[] args){
            string tooFewArgs = "ОШИБКА: Недостаточно аргументов.\r\nВведите \"exbat --help\" для справки.";

            if(args.Length == 0){
                Console.WriteLine(tooFewArgs);
                Environment.Exit(1);
            }

            if(args[0] == "--help"){
                Console.WriteLine("exbat [-c,--compile <inputFile> <outputFile>] [-r,--run <inputFile>]\r\n\r\n\t-c,--compile <inputFile> <outputFile>        Компилирует скрипт ExBAT.\r\n\t-r,--run <inputFile>                         Компилирует и выполняет скрипт ExBAT.");
            }

            if(args[0] == "-c" || args[0] == "--compile"){
                if(args.Length < 3){
                    Console.WriteLine(tooFewArgs);
                    Environment.Exit(1);
                }
                
                if(!File.Exists(args[1])){
                    Console.WriteLine("ОШИБКА: Файл не найден.");
                    Environment.Exit(0);
                }

                File.WriteAllText(args[2], Compile(File.ReadAllText(args[1])));
            }

            if(args[0] == "-r" || args[0] == "--run"){
                if(args.Length < 2){
                    Console.WriteLine(tooFewArgs);
                    Environment.Exit(1);
                }

                if(!File.Exists(args[1])){
                    Console.WriteLine("ОШИБКА: Файл не найден.");
                    Environment.Exit(0);
                }

                string path = Environment.GetEnvironmentVariable("temp")+"\\"+GetRandomName(20)+".bat";
                File.WriteAllText(path, Compile(File.ReadAllText(args[1])));
                Process.Start(path).WaitForExit();
                File.Delete(path);
            }
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
                string lts = line.TrimStart();

                if(lts == "purebatch"){
                    parse = true;
                }else if(lts == "end purebatch"){
                    parse = false;
                }

                if(!parse) continue;

                if(line.StartsWith("#") || line.StartsWith("//")){
                    fc[i] = line.Replace("#", "rem ").Replace("//", "rem ");
                }else if(lts.StartsWith("noout(")){
                    fc[i] = line.Remove(line.Length-1).Replace("noout(", "")+"> nul";
                }else if(lts.StartsWith("noerror(")){
                    fc[i] = line.Remove(line.Length-1).Replace("quiet(", "")+"2> nul";
                }else if(lts.StartsWith("quiet(")){
                    fc[i] = line.Remove(line.Length-1).Replace("quiet(", "")+"> nul 2> nul";
                }else if(lts.StartsWith("if(")){
                    string cond = Regex.Match(line, @"if\(([^\)]+)\)").Groups[1].ToString();
                    fc[i] = "if "+cond.Replace("<=", "leq").Replace(">=", "geq").Replace("<", "lss").Replace(">", "gtr").Replace(" == ", " equ ")+" (";
                }else if(lts == "end if"){
                    fc[i] = ")";
                }else if(lts == "else"){
                    fc[i] = ") else (";
                }else if(lts.StartsWith("else if(")){
                    string cond = Regex.Match(line, @"else if\(([^\)]+)\)").Groups[1].ToString();
                    fc[i] = ") else if "+cond.Replace("<=", "leq").Replace(">=", "geq").Replace("<", "lss").Replace(">", "gtr").Replace(" == ", " equ ")+" (";
                }else if(lts.StartsWith("function ")){
                    string[] spl = line.Replace("(", " ").Replace(")", "").Replace(", ", " ").Replace(",", " ").Split(' ');
                    isFunc = true;
                    fnn = spl[1];
                    foreach(string fa in spl.Skip(2)) fna.Add(fa);
                    fc[i] = ":"+fnn+Environment.NewLine+"if \"%~1\"==\""+randFuncArg+"\" ( shift /1 ) else ( goto _"+randName+"_end_"+fnn+" )";
                    fna.Add(fnn);
                }else if(lts == "end function"){
                    isFunc = false;
                    fna.Clear();
                    fc[i] = "goto :eof"+Environment.NewLine+":_"+randName+"_end_"+fnn;
                    fnn = "";
                }else if(Regex.IsMatch(lts, @"^[A-Za-z0-9_\-\.]+\(.*\)$")){
                    var m = Regex.Match(lts, @"^([A-Za-z0-9_\-\.]+)\((.*)\)$");
                    fc[i] = "call :"+m.Groups[1].ToString()+" "+randFuncArg+" "+m.Groups[2].ToString().Replace(", ", " ").Replace(",", " ").Replace(" out ", " ");
                }

                if(isFunc){
                    for(int j = 0; j < fna.Count; j++){
                        fc[i] = fc[i].Replace("$"+fna[j], "%~"+(j+1).ToString());
                    }
                }

                fc[i] = Regex.Replace(fc[i], @"\$([A-Za-z0-9_\.\[\]]+)", new MatchEvaluator((m)=>{
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
