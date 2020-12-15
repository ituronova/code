#!/usr/bin/env python3

import argparse
import csv
import sys
from tabulate import tabulate

f = open('pg3200.txt', 'r')
lines = f.readlines()
mystr = '\t'.join([line.strip() for line in lines])
g = open('pg3200-singleLine.txt', 'w')
g.write(mystr)
f.close()
g.close()