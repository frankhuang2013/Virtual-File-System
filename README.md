# Virtual-File-System
A Linux Ext-like file system run on a file. Supports basic directory hierarchy, inode structure, broken file recovery. Also supports network disk mounting so that different users with multiple disks can transfer files synchronously.





Error Code:
1. Failed to create disk: Wrong FS version number.
2. Failed to create disk: Disk capacity is lower than one data block.
3. Failed to create disk: Disk capacity doesn't make integer number of blocks.
4. Failed to create disk: Disk name length is greater than 256 Bytes.
5. Defined disk name contains invalid character(s).
6. Failed to create disk file.
7. 













7. Failed to create disk file.
8. Failed to open Disk File when initializing.
9. Invalid disk: invalid verification code.
10. Invalid disk: invalid terminal code.
11. Invalid disk: Unexpected data in the end of the FS.
12. Calling PFS::init() at a wrong time.
13. Failed to open disk file when making new directory.
14. Cannot create new directory: Disk full.
15. Invalid path: Path contains unfound directory or file.
16. Cannot apply for new space when updating directory record: Disk full.
17. Failed to open disk file when making new file.
18. Cannot create new file: Disk full.
19. Cannot create new directory: Path locates to a file.
20. Cannot create new file: Path locates to a file.
21. Cannot create directory: Invalid directory name.
22. Cannot create file: Invalid file name.
23. Failed to open disk file when listing.
24. Failed to list: Trying to list file.
25. Invalid Path: Lack of authority.
26. Cannot create new directory: Lack of authority.
27. Cannot create new file: Lack of authority.
28. Cannot upload file: Invalid file name.
29. Failed to open disk file when uploading file.
30. Failed to open source file when uploading file.
31. Cannot upload file: No enough disk space.
32. Cannot upload file: Path locates to a file.
33. Cannot upload file: Lack of authority.
34. Failed to open disk file when downloading file.
35. Cannot download file: Path locates to a file.
36. Cannot download file: File not found.
37. Cannot download file: Lack of authority.
38. Cannot download file: filename is a directory.
39. Cannot download file: Cannot open local path file.
40. Failed to open disk file when removing file.
41. Cannot remove file: Path locates to a file.
42. Cannot remove file: File not found.
43. Cannot remove file: filename is a directory.
44. Cannot remove file: Lack of authority.
45. Failed to open disk file when getting directory index.
46. Cannot get directory index: Path locates to a file.
47. Failed to open disk file when getting disk info.
48. Failed to get disk info: invalid disk verification code.
49. Failed to get disk info: invalid disk terminal code.
50. Cannot apply for new space when updating directory record: Name collision.
51. Failed to open disk file when removing directory.
52. Cannot remove directory: Path locates to a file.
53. Cannot remove directory: Lack of authority.
54. Cannot remove directory: directory not found.
55. Cannot remove directory: filename is a file.
56. Cannot remove directory: directory is not empty.
57. Cannot update file: Invalid file name.
58. Failed to open disk file when updating file.
59. Cannot update file: Path locates to a file.
60. Cannot update file: Lack of authority.
61. Cannot update file: File not found.
62. Cannot update file: filename is a directory.
63. Failed to open disk file when moving item.
64. Cannot move file: Lack of authority.
65. Cannot move file: File not found.
66. Cannot apply for new space when updating directory record: Invalid name.
67. Cannot rename the disk: Invalid name.
68. Cannot rename the disk: Name too long.
69. Cannot rename the disk: Lack of authority.
70. Failed to open disk file when renaming the disk.
71. Failed to open disk file when removing the directory recursively.


Footnote:
1. Invalid character in a name: /\:*?”<>|
2. file property: (directory/file)(system/non system)rrrrrrrrrrrrrr
3. file authority: uuugggooorrrrrrr

