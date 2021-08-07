# --*-- coding: utf-8 --*--

import os
import datetime
import sys

WORK_PATH = os.getcwd()
LOG_PATH = os.path.join(WORK_PATH, 'Logs')

class Logger(object):
	def __init__(self, file_name):
		if not os.path.exists(LOG_PATH):
			os.makedirs(LOG_PATH)

		date = datetime.datetime.now()
		self.file_path = os.path.join(LOG_PATH, '{} [{}_{}_{}-{}_{}_{}].txt'.format(file_name, date.year, date.month, date.day, date.hour, date.minute, date.second))

	def log(self, content):
		with open(self.file_path, 'a') as f:
			f.write(content)
			f.write('\n')

