#!/usr/bin/python3
import os
from twisted.python.filepath import FilePath
from subprocess import Popen
import re
import sys
from xml.dom.minidom import parse as XMLOpen

tm = '-m' in sys.argv

parallel = '-j' in sys.argv

PROJECT_PATTERN = re.compile(r'''Project.".[0-9A-Za-z]{8}-[0-9A-Za-z]{4}-[0-9A-Za-z]{4}-[0-9A-Za-z]{4}-[0-9A-Za-z]{12}.". = '''
                             r'''"([0-9a-zA-Z]+Compat)", '''
                             r'''"([0-9A-zA-Z\/]+.csproj)", '''
                             r'''".[0-9A-Za-z]{8}-[0-9A-Za-z]{4}-[0-9A-Za-z]{4}-[0-9A-Za-z]{4}-[0-9A-Za-z]{12}."''')

PUBLICIZER = os.environ.get("PUBLICIZER", "./AssemblyPublicizer")
DOWNLOAD_LIBS = os.environ.get("DOWNLOAD_LIBS", "--download-libs")
tasks = []

def system(*cmd):
    sp = Popen(cmd)
    if not parallel:
        sp.wait()
        ec = sp.poll()
        if ec:
            raise SystemExit(ec)
    else:
        tasks.append(sp)

with open("Source/CombatExtended.sln") as f:
    for line in f.readlines():
        line = line.strip()
        match = PROJECT_PATTERN.match(line)
        if match:
            name, csproj = match.groups()
            if tm and name not in sys.argv: continue
            csproj = csproj.replace('\\', '/').split('/')
            csproj = FilePath("Source").descendant(csproj)
            output = FilePath("AssembliesCompat").child(name+".dll")
            with XMLOpen(csproj.path) as cpath:
                op = cpath.getElementsByTagName("OutputPath")
                if op:
                    op = op[0].firstChild.data
                    if 'ModPatches' in op:
                        op = op.rsplit("..\\ModPatches", 1)[-1].replace('\\', '/').split('/')
                        if op:
                            od = FilePath("ModPatches").descendant(op)
                            if not od.exists():
                                od.makedirs()
                            output = od.child(name+".dll")

            print(f"Building {name}")
            system("python3", "Make.py", "--csproj", csproj.path, "--output", output.path, DOWNLOAD_LIBS, "--all-libs", "--publicizer", PUBLICIZER, "--", "-r:Assemblies/CombatExtended.dll")

for t in tasks:
    t.wait()
