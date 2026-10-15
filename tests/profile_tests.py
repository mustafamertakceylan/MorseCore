"""Foreign-consumer checks for the additive Unicode alphabet API."""
import concurrent.futures
import ctypes as c
from pathlib import Path
import sys
import unittest

dll = c.CDLL(str(Path(sys.argv.pop(1)).resolve()))
U32,U64,P=c.c_uint32,c.c_uint64,c.c_void_p
class Stats(c.Structure):
    _fields_=[(n,U64) for n in ('input','output','words','converted','spaces')]
class Symbol(c.Structure):
    _fields_=[('character',U32),('extension',U32),('code',c.c_char*8)]
def bind(name,args,result=c.c_int32):
    f=getattr(dll,name); f.argtypes=args; f.restype=result; return f
encode=bind('mc_encode_alphabet',[c.c_char_p,U64,U32,U32,c.POINTER(P)])
decode=bind('mc_decode_alphabet',[c.c_char_p,U64,U32,U32,c.POINTER(P)])
destroy=bind('mc_result_destroy',[P],None)
copy=bind('mc_result_copy_text',[P,P,U64,c.POINTER(U64)])
stats=bind('mc_result_statistics',[P,c.POINTER(Stats),U32])
count=bind('mc_alphabet_profile_count',[U32],U32)
entry=bind('mc_alphabet_profile_get',[U32,U32,c.POINTER(Symbol),U32])
def convert(text,profile,reverse=False,separator='/'):
    data=text.encode() if isinstance(text,str) else text
    handle=P(); status=(decode if reverse else encode)(data,len(data),ord(separator),profile,c.byref(handle))
    if status: assert not handle.value; return status,None
    try:
        required=U64(); assert copy(handle,None,0,c.byref(required))==-101
        buffer=c.create_string_buffer(required.value)
        assert copy(handle,buffer,len(buffer),c.byref(required))==0
        s=Stats(); assert stats(handle,c.byref(s),c.sizeof(s))==0
        return buffer.value.decode(),s
    finally: destroy(handle)

class Profiles(unittest.TestCase):
    def test_known_russian(self):
        output,s=convert('Привет, мир!',1)
        self.assertEqual(output,'.--. .-. .. .-- . - --..-- / -- .. .-. -.-.-- ')
        self.assertEqual((s.input,s.words,s.converted,s.spaces),(12,2,11,1))
        self.assertEqual(convert(output,1,True)[0],'ПРИВЕТ, МИР!')
    def test_known_arabic(self):
        self.assertEqual(convert('سلام',2)[0],'... .-.. .- -- ')
        self.assertEqual(convert('... .-.. .- -- ',2,True)[0],'سلام')
    def test_marks_and_spaces(self):
        output,s=convert('سَلَامٌ  عالم',2)
        self.assertEqual(convert(output,2,True)[0],'سلام عالم')
        self.assertEqual((s.input,s.words,s.converted,s.spaces),(13,2,8,2))
    def test_normalizations(self):
        self.assertEqual(convert(convert('Ёж ёлка',1)[0],1,True)[0],'ЕЖ ЕЛКА')
        self.assertEqual(convert(convert('أإآٱىةؤئ ١٢،؟',2)[0],2,True)[0],'اااايهوي 12,?')
    def test_every_symbol(self):
        self.assertEqual([count(p) for p in range(4)],[54,60,57,0])
        for profile in range(3):
            seen=set()
            for index in range(count(profile)):
                symbol=Symbol(); self.assertEqual(entry(profile,index,c.byref(symbol),16),0)
                text=chr(symbol.character)
                self.assertNotIn(bytes(symbol.code),seen); seen.add(bytes(symbol.code))
                self.assertEqual(convert(convert(text,profile)[0],profile,True)[0],text)
    def test_same_signal_different_alphabet(self):
        self.assertEqual([convert('.-',p,True)[0] for p in range(3)],['A','А','ا'])
    def test_errors(self):
        for value,status in [(b'\xff',-105),(b'a\0b',-2),('A',-3),('😀',-3),('َـ',-1)]:
            self.assertEqual(convert(value,2)[0],status)
        handle=P(1); self.assertEqual(encode(b'a',1,47,99,c.byref(handle)),-100); self.assertFalse(handle.value)
        self.assertEqual(convert('.-.-.-.-',1,True)[0],-4)
    def test_unicode_word_gaps(self):
        output,s=convert('мир\u00a0мир',1)
        self.assertEqual((s.input,s.words,s.converted),(7,2,6))
        self.assertEqual(convert(output,1,True)[0],'МИР МИР')
    def test_bom_and_separators(self):
        for profile,text in [(1,'МИР МИР'),(2,'سلام سلام')]:
            for separator in '/|#*':
                output,_=convert('\ufeff'+text,profile,separator=separator)
                self.assertEqual(convert(output,profile,True,separator)[0],text)
    def test_concurrent_and_large(self):
        def run(profile):
            text=('ПРИВЕТ ' if profile==1 else 'سلام ')*1000
            return convert(convert(text,profile)[0],profile,True)[0]==text.rstrip()
        with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
            self.assertTrue(all(pool.map(run,[1,2]*8)))

if __name__=='__main__': unittest.main()
