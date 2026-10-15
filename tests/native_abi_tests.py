"""Exercise the actual shared library across a foreign ABI, without GUI/audio devices."""
import concurrent.futures
import ctypes as c
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import threading
import unittest
import wave

LIBRARY = Path(sys.argv.pop(1)).resolve()
DLL = c.CDLL(str(LIBRARY))
U32, U64, I32, P = c.c_uint32, c.c_uint64, c.c_int32, c.c_void_p
class Stats(c.Structure):
    _fields_ = [(n, U64) for n in ('input', 'output', 'words', 'converted', 'spaces')]
class Symbol(c.Structure):
    _fields_ = [('character', U32), ('extension', U32), ('code', c.c_char * 8)]
class PracticeStats(c.Structure):
    _fields_ = [(n, U32) for n in ('total', 'answered', 'correct', 'incorrect', 'skipped', 'mode')] + [('accuracy', c.c_double)]
class Feedback(c.Structure):
    _fields_ = [('verdict', I32), ('number', U32), ('answer', c.c_char * 64)]
class Options(c.Structure):
    _fields_ = [(n, U32) for n in ('wpm', 'frequency', 'rate', 'reserved')]
class Analysis(c.Structure):
    _fields_ = [(n, U64) for n in ('units', 'samples', 'tones')]

def bind(name, args, result=I32):
    function = getattr(DLL, name)
    function.argtypes, function.restype = args, result
    return function

abi = bind('mc_abi_version', [], U32)
encode = bind('mc_encode', [c.c_char_p, U64, U32, c.POINTER(P)])
decode = bind('mc_decode', [c.c_char_p, U64, U32, c.POINTER(P)])
destroy = bind('mc_result_destroy', [P], None)
copy_text = bind('mc_result_copy_text', [P, P, U64, c.POINTER(U64)])
result_stats = bind('mc_result_statistics', [P, c.POINTER(Stats), U32])
table_count = bind('mc_alphabet_count', [], U32)
table_get = bind('mc_alphabet_get', [U32, c.POINTER(Symbol), U32])
practice_create = bind('mc_practice_create', [U32, U32, U32, c.POINTER(P)])
practice_destroy = bind('mc_practice_destroy', [P], None)
practice_next = bind('mc_practice_next', [P, c.POINTER(P)])
practice_submit = bind('mc_practice_submit', [P, c.c_char_p, U64, c.POINTER(Feedback), U32])
practice_stats = bind('mc_practice_stats', [P, c.POINTER(PracticeStats), U32])
defaults = bind('mc_audio_defaults', [c.POINTER(Options), U32])
analyze = bind('mc_audio_analyze', [c.c_char_p, U64, U32, c.POINTER(Options), U32, c.POINTER(Analysis), U32])
wav = bind('mc_write_wav', [c.c_char_p, c.c_char_p, U64, U32, c.POINTER(Options), U32])
audio_error = bind('mc_audio_last_error', [], c.c_char_p)
cancel_create = bind('mc_cancel_create', [c.POINTER(P)])
cancel_request = bind('mc_cancel_request', [P], None)
cancel_destroy = bind('mc_cancel_destroy', [P], None)
play = bind('mc_play', [c.c_char_p, U64, U32, c.POINTER(Options), U32, P])

def text_from(handle):
    required = U64()
    assert copy_text(handle, None, 0, c.byref(required)) == -101
    buffer = c.create_string_buffer(required.value)
    assert copy_text(handle, buffer, len(buffer), c.byref(required)) == 0
    return buffer.value.decode('utf-8')

def conversion(text, decoder=False, separator=0):
    data = text.encode('utf-8') if isinstance(text, str) else text
    handle = P()
    status = (decode if decoder else encode)(data, len(data), separator, c.byref(handle))
    assert status == 0, status
    try:
        result = text_from(handle)
        stats = Stats()
        assert result_stats(handle, c.byref(stats), c.sizeof(stats)) == 0
        return result, stats
    finally:
        destroy(handle)

class NativeAbiTests(unittest.TestCase):
    def test_layout_and_version(self):
        self.assertEqual(abi(), 1)
        self.assertEqual([c.sizeof(t) for t in (Stats, Symbol, PracticeStats, Feedback, Options, Analysis)], [40, 16, 32, 72, 16, 24])

    def test_unicode_stats_and_all_separators(self):
        for separator in '/|#*':
            encoded, stats = conversion('Merhaba Dünya!', separator=ord(separator))
            self.assertEqual((stats.input, stats.words, stats.converted, stats.spaces), (14, 2, 13, 1))
            self.assertEqual(conversion(encoded, decoder=True)[0], 'MERHABA DUNYA!')

    def test_alphabet_roundtrip_and_extensions(self):
        self.assertEqual(table_count(), 54)
        extensions = []
        for index in range(table_count()):
            symbol = Symbol()
            self.assertEqual(table_get(index, c.byref(symbol), c.sizeof(symbol)), 0)
            original = chr(symbol.character)
            self.assertEqual(conversion(conversion(original)[0], decoder=True)[0], original)
            if symbol.extension: extensions.append(original)
        self.assertEqual(''.join(extensions), '!&;_$')
        self.assertEqual(table_get(54, c.byref(Symbol()), 16), -100)

    def test_large_roundtrip(self):
        original = 'Z' * (2 * 1024 * 1024 + 1)
        self.assertEqual(conversion(conversion(original)[0], decoder=True)[0], original)

    def test_strict_utf8_bom_and_nul(self):
        self.assertEqual(conversion(b'\xef\xbb\xbfSOS')[1].input, 3)
        for data, expected in ((b'\xc7', -105), (b'\xc0\xaf', -105), (b'\xed\xa0\x80', -105),
                               (b'A\0B', -2), ('🙂'.encode(), -3), (b'%', -3), (b'', -1)):
            handle = P(1)
            self.assertEqual(encode(data, len(data), 0, c.byref(handle)), expected)
            self.assertIsNone(handle.value)

    def test_overflow_null_and_bad_separator(self):
        handle = P(1)
        self.assertEqual(encode(b'A', 2**64-1, 0, c.byref(handle)), -6)
        self.assertIsNone(handle.value)
        self.assertEqual(encode(None, 1, 0, c.byref(handle)), -100)
        self.assertEqual(encode(b'A', 1, 2**32-1, c.byref(handle)), -20)
        self.assertEqual(encode(b'A', 1, 0, None), -100)

    def test_output_capacity_and_struct_canaries(self):
        handle = P()
        self.assertEqual(encode(b'SOS', 3, 0, c.byref(handle)), 0)
        try:
            buffer = c.create_string_buffer(b'canary')
            required = U64()
            self.assertEqual(copy_text(handle, buffer, 1, c.byref(required)), -101)
            self.assertEqual(buffer.value, b'canary')
            self.assertEqual(required.value, 13)
            stats = Stats(11, 12, 13, 14, 15)
            self.assertEqual(result_stats(handle, c.byref(stats), 39), -100)
            self.assertEqual(stats.input, 11)
        finally:
            destroy(handle)

    def test_concurrent_conversions(self):
        def worker(index):
            expected = f'SOS {index}'
            return conversion(conversion(expected)[0], decoder=True)[0] == expected
        with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
            self.assertTrue(all(pool.map(worker, range(200))))

    def test_practice_state_feedback_and_isolation(self):
        first, second = P(), P()
        self.assertEqual(practice_create(0, 3, 123, c.byref(first)), 0)
        self.assertEqual(practice_create(0, 3, 123, c.byref(second)), 0)
        try:
            feedback = Feedback()
            self.assertEqual(practice_submit(first, b'A', 1, c.byref(feedback), 72), -102)
            for index in range(3):
                handles = [P(), P()]
                for session, result in zip((first, second), handles):
                    self.assertEqual(practice_next(session, c.byref(result)), 0)
                try:
                    morse = text_from(handles[0])
                    self.assertEqual(morse, text_from(handles[1]))
                    answer = conversion(morse, decoder=True)[0].lower().encode()
                finally:
                    for handle in handles: destroy(handle)
                expected = answer if index == 0 else (b'%' if index == 1 else b'')
                self.assertEqual(practice_submit(first, expected, len(expected), c.byref(feedback), 72), 0)
                self.assertEqual(feedback.verdict, (1, 0, 2)[index])
                self.assertEqual(feedback.answer.decode().lower(), answer.decode())
                self.assertEqual(practice_submit(first, expected, len(expected), c.byref(feedback), 72), -102)
                self.assertEqual(practice_submit(second, None, 0, c.byref(feedback), 72), 0)
            stats = PracticeStats()
            self.assertEqual(practice_stats(first, c.byref(stats), 32), 0)
            self.assertEqual((stats.answered, stats.correct, stats.incorrect, stats.skipped), (3, 1, 1, 1))
            self.assertAlmostEqual(stats.accuracy, 100/3)
        finally:
            practice_destroy(first); practice_destroy(second)

    def test_audio_layout_timing_and_utf8_path(self):
        options, analysis = Options(), Analysis()
        self.assertEqual(defaults(c.byref(options), 16), 0)
        self.assertEqual((options.wpm, options.frequency, options.rate, options.reserved), (20, 600, 22050, 0))
        morse = b'... --- ...'
        self.assertEqual(analyze(morse, len(morse), 0, None, 0, c.byref(analysis), 24), 0)
        self.assertEqual((analysis.units, analysis.samples, analysis.tones), (27, 35721, 9))
        with tempfile.TemporaryDirectory(dir=LIBRARY.parent) as directory:
            path = Path(directory) / 'Çığ ses.wav'
            self.assertEqual(wav(str(path).encode(), morse, len(morse), 0, c.byref(options), 16), 0)
            with wave.open(str(path)) as file:
                self.assertEqual((file.getnchannels(), file.getsampwidth(), file.getnframes()), (1, 2, 35721))

    def test_thread_local_audio_errors(self):
        barrier = threading.Barrier(2)
        def worker(wpm, frequency, expected):
            options, analysis = Options(wpm, frequency, 22050, 0), Analysis()
            status = analyze(b'.', 1, 0, c.byref(options), 16, c.byref(analysis), 24)
            barrier.wait(timeout=10)
            return status == -103 and expected in audio_error().decode('utf-8')
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            one = pool.submit(worker, 1, 600, 'speed')
            two = pool.submit(worker, 20, 1, 'frequency')
            self.assertTrue(one.result()); self.assertTrue(two.result())

    def test_pre_cancelled_audio_no_device(self):
        cancellation = P()
        self.assertEqual(cancel_create(c.byref(cancellation)), 0)
        try:
            with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
                list(pool.map(lambda _: cancel_request(cancellation), range(20)))
            self.assertEqual(play(b'.', 1, 0, None, 0, cancellation), -104)
        finally:
            cancel_destroy(cancellation)

    def test_loading_has_no_runtime_home_side_effects(self):
        with tempfile.TemporaryDirectory(dir=LIBRARY.parent) as directory:
            source = 'import ctypes,sys; d=ctypes.CDLL(sys.argv[1]); assert d.mc_abi_version()==1'
            process = subprocess.run([sys.executable, '-c', source, str(LIBRARY)], cwd=directory,
                env={**os.environ, 'MORSECORE_HOME': directory+'/missing/invalid'}, capture_output=True, timeout=20)
            self.assertEqual(process.returncode, 0, process.stderr.decode(errors='replace'))
            self.assertEqual(process.stdout, b'')
            self.assertEqual(list(Path(directory).iterdir()), [])

if __name__ == '__main__':
    unittest.main(verbosity=2)
