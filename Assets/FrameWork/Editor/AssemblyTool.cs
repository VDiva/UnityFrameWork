using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;



namespace FrameWork
{
    [InitializeOnLoad]
    public static class AssemblyTool
    {
        
        static AssemblyTool()
        {
            InitAssemblySet();
        }

        [RuntimeInitializeOnLoadMethod]
        [MenuItem("FrameWork/AssemblySet")]
        public static void InitAssemblySet()
        {
            try
            {
                //Debug.Log("编辑程序集中....");

                // Lock assemblies while they may be altered
                EditorApplication.LockReloadAssemblies();

                // 程序集路径
                HashSet<string> assemblyPaths = new HashSet<string>();
                // 程序集文件夹名字
                //HashSet<string> assemblySearchDirectories = new HashSet<string>();


                //获得app所有程序集
                foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.Location.Replace('\\', '/')
                        .StartsWith(Application.dataPath.Substring(0, Application.dataPath.Length - 7)))
                    {
                        assemblyPaths.Add(assembly.Location);
                    }
                }
                
                
                WriterParameters writerParameters = new WriterParameters();
                foreach (String assemblyPath in assemblyPaths)
                {
                    AssemblyDefinition assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyPath);
                    if (AssemblyTool.ProcessAssembly(assemblyDefinition))
                    {
                        Debug.Log("更新到程序集:" + assemblyPath);
                        assemblyDefinition.Write(assemblyPath, writerParameters);
                        
                    }
                    else
                    {
                        
                    }
                }
                
                EditorApplication.UnlockReloadAssemblies();
            }
            catch (Exception e)
            {
                Debug.LogWarning(e);
            }
            finally
            {
                
            }
        }

        private static bool ProcessAssembly( AssemblyDefinition assemblyDefinition )
        {
            bool wasProcessed = false;

            foreach( ModuleDefinition moduleDefinition in assemblyDefinition.Modules )
            {
                foreach( TypeDefinition typeDefinition in moduleDefinition.Types )
                {
                    foreach( MethodDefinition methodDefinition in typeDefinition.Methods )
                    {
                        //CustomAttribute logAttribute = null;

                        foreach( CustomAttribute customAttribute in methodDefinition.CustomAttributes )
                        {
                            if(Intercept(moduleDefinition,methodDefinition,customAttribute.AttributeType.FullName.Split(".").Last()))
                            {
                                methodDefinition.CustomAttributes.Remove( customAttribute );
                                wasProcessed = true;
                            }
                        }
                        
                    }
                }
            }
            
            return wasProcessed;
        }


        private static bool Intercept(ModuleDefinition moduleDefinition,MethodDefinition methodDefinition,string attributeName)
        {
            bool isHas = false;
            switch (attributeName)
            {
                case "LogAttribute":
                    isHas = true;
                    InitLog(moduleDefinition,methodDefinition,attributeName);
                    break;
            }

            return isHas;
        }



        private static void InitLog(ModuleDefinition moduleDefinition,MethodDefinition methodDefinition,string name)
        {
            
            MethodReference logMethodReference = moduleDefinition.Import( typeof( Debug ).GetMethod( "Log", new Type[] { typeof( object ) } ) );
            
            ILProcessor ilProcessor = methodDefinition.Body.GetILProcessor();

            //ilProcessor.Emit(OpCodes.Ldarg_1);
            
            
            Instruction first = methodDefinition.Body.Instructions[0];
            ilProcessor.InsertBefore( first, Instruction.Create( OpCodes.Ldstr, "LOG 进入方法:" + name + "." + methodDefinition.Name ) );
            ilProcessor.InsertBefore( first, Instruction.Create( OpCodes.Call, logMethodReference ) );
            
            Instruction last = methodDefinition.Body.Instructions[methodDefinition.Body.Instructions.Count - 1];
            ilProcessor.InsertBefore( last, Instruction.Create( OpCodes.Ldstr, "LOG 退出方法:" + name + "." + methodDefinition.Name ) );
            ilProcessor.InsertBefore( last, Instruction.Create( OpCodes.Call, logMethodReference ) );
        }
    }
}

