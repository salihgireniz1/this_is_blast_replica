"""serve_test.py - the one guard in serve.py that matters: which names it will write.

Run with:  python -m unittest level-editor/serve_test.py   (from the repo root)
Everything else in serve.py is Python's own HTTP server handing files around.
"""

import unittest
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from serve import safe_level_name  # noqa: E402


class SafeLevelName(unittest.TestCase):
    """A request may name one .json file in the folder and nothing else."""

    def test_a_plain_level_name_passes(self):
        self.assertEqual(safe_level_name("/api/levels/Level_06.json"), "Level_06.json")

    def test_url_encoding_is_undone(self):
        self.assertEqual(safe_level_name("/api/levels/My%20Level.json"), "My Level.json")

    def test_a_path_that_climbs_out_of_the_folder_is_refused(self):
        self.assertIsNone(safe_level_name("/api/levels/../ProjectSettings/ProjectVersion.txt"))
        self.assertIsNone(safe_level_name("/api/levels/..%2F..%2Fsecret.json"))
        self.assertIsNone(safe_level_name("/api/levels/sub\\dir.json"))

    def test_anything_but_a_json_file_is_refused(self):
        self.assertIsNone(safe_level_name("/api/levels/notes.txt"))
        self.assertIsNone(safe_level_name("/api/levels/.json"))
        self.assertIsNone(safe_level_name("/api/levels/"))
        self.assertIsNone(safe_level_name("/other/Level_01.json"))


if __name__ == "__main__":
    unittest.main()
