\ MAKEDAWG.F
\
\  make-dawg - converts the file "words.txt" of lowercase words,
\              sorted alphabetically one per line, into "dawg.out"
include dawg.f

\ utility
: CELL/   2 RSHIFT ;

16 CONSTANT max-word-len   \ max len in a Scrabble dictionary is 15
VARIABLE word-len
CREATE next-word max-word-len CHARS ALLOT
VARIABLE prefix-len
CREATE prefix max-word-len CHARS ALLOT

: prefix-len+  prefix-len @ CHARS + ;

: next-word-has-prefix? ( -- nz )
  next-word prefix-len @ prefix OVER COMPARE 0= word-len @ AND ;

\
\ DAWG builder
\

VARIABLE words-file
VARIABLE dawg-file
VARIABLE cur-dawg-index

: get-next-word
  next-word max-word-len CHARS words-file @ READ-LINE 2DROP word-len ! ;

: write-to-dawg ( block size -- )
  dawg-file @ WRITE-FILE ABORT" Can't write to dawg!" ;

\
\ Hash Table for blocks
\
2311 CONSTANT hash-size
VARIABLE htab

: create-hash-table
  hash-size CELLS ALLOCATE ABORT" Hash table too big!"
  DUP hash-size CELLS ERASE htab ! ;

: htab@i ( hash-index -- head-entry-addr ) CELLS htab @ + ;
: ->next ; IMMEDIATE
: ->index CELL+ ;
: ->block CELL+ CELL+ ;

: destroy-hash-table
  htab @ hash-size 0
  DO   DUP @
       BEGIN  ?DUP 
       WHILE  DUP ->next @  SWAP FREE DROP
       REPEAT CELL+
  LOOP DROP htab @ FREE DROP ;

\ 0 for a trie, >5 for a dawg (measured no dups above size 5)
6 CELLS CONSTANT Block-size-hash-threshold

: hash-block ( block -- hash )
  0 >R CELL-
  BEGIN CELL+ DUP @ DUP R> XOR >R EOB UNTIL
  DROP R> hash-size MOD ;

: blocks-equivalent? ( block1 block2 -- TF )
  BEGIN  OVER @ OVER @ <> IF 2DROP FALSE EXIT THEN
         DUP @ EOB 0=
  WHILE  CELL+ SWAP CELL+
  REPEAT 2DROP TRUE ;

: find-hash-block ( block -- index | 0 )
  DUP hash-block htab@i    ( block hash-block-addr )
  BEGIN @ DUP
  WHILE 2DUP ->block blocks-equivalent?
        IF ->index @ NIP EXIT
        THEN ->next
  REPEAT NIP ( 0 ) ;

: add-hash-block ( size block -- )
  OVER ->block ALLOCATE ABORT" Can't allocate hash entry!" 
  OVER hash-block htab@i           ( size block h head-addr )
  2DUP @ OVER ->next ! SWAP !      \ replace the head <- h
  cur-dawg-index @ OVER ->index !
  ->block ROT MOVE ;

\ Core DAWG building algorithm

: index-for-block ( size block -- index )
  OVER Block-size-hash-threshold <
  IF   DUP find-hash-block ?DUP IF NIP NIP EXIT THEN
       2DUP add-hash-block
  THEN
  OVER write-to-dawg  ( size )
  CELL/ cur-dawg-index @ TUCK + cur-dawg-index ! ;

: append-next-letter-to-prefix ( -- a-z )
  next-word prefix-len+ C@
  prefix prefix-len+ 2DUP C@ <=
  IF 2DROP ABORT" Words out of order!" THEN
  2DUP C!  0 SWAP CHAR+ C!
  1 prefix-len +! ;

: init-node-with-letter ( node a-z -- node )
  c>let InitLet OVER !
  word-len @ prefix-len @ =
  IF   EOW_MASK OVER +!
       get-next-word
  THEN ;

: remove-letter-from-prefix   -1 prefix-len +! ;

: finish-block ( prefix-node last-node -- prefix-node )
  2DUP = IF DROP EXIT THEN
  EOB_MASK OVER +!
  OVER - OVER CELL+ 	( prefix size block )
  index-for-block OVER +! ;

: suffixes ( prefix-node-addr -- prefix-node-addr )
  DUP 	   ( prefix current )
  BEGIN  next-word-has-prefix?
  WHILE  CELL+              \ allocate a new node
         append-next-letter-to-prefix
         init-node-with-letter
         RECURSE			\ process all suffixes from this prefix
         remove-letter-from-prefix
  REPEAT   ( prefix last )
  finish-block ;

: make-dawg
  S" words.txt" R/O OPEN-FILE ABORT" No input file!" words-file !
  S" dawg.out" R/W CREATE-FILE ABORT" No output file!" dawg-file !
  create-hash-table
  100 CELLS ALLOCATE ABORT" Can't allocate block stack!"
  ( blocks ) \ max 87 for "outstunting"

  0 OVER ! DUP 4 write-to-dawg	\ skip root pointer
  1 cur-dawg-index !
  get-next-word
  0 prefix-len !  0 prefix C!
  suffixes  ( blocks[0] filled with root node index )
  0. dawg-file @ REPOSITION-FILE ABORT" Can't rewind!"
  DUP 4 write-to-dawg	\ backpatch root pointer

  FREE DROP  destroy-hash-table
  dawg-file @ CLOSE-FILE DROP
  words-file @ CLOSE-FILE DROP ;
