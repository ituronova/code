import csv
import glob
import os
import sys
import re



def createScripts(r1, name):
    f = open("test_2.txt", "a", newline='')
    index = 0
    for row1 in r1:
       for index in range(0,len(row1)):
           f.write(row1[index:])
          
        
    return;
    
name = "test_1.txt"
f2 = open(name, "r")
base=os.path.basename(name)
createScripts(f2, base)
    
   