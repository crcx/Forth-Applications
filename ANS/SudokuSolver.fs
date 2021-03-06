\ utility

: pow2 ( n -- 2^n ) 1 swap lshift ;

: bits ( mask -- n )
  0 begin swap dup while dup 1- and  swap 1+ repeat drop ;

: log2 ( mask -- n )
  0 begin swap dup while 2/ swap 1+ repeat drop ;

\ 0 constant empty

              3 constant sqSize
sqSize sqSize * constant dim
      dim dim * constant size

: fillWithString ( addr len dest -- ) over size <> abort" wrong size!"
  tuck >R move
  R@ size + R> do i c@ [char] 0 - i c! loop ;

create sqMap size allot
s" 000111222000111222000111222333444555333444555333444555666777888666777888666777888"
  sqMap fillWithString

create sqInds size allot   \ square number to indexes (inverse of sqMap)
: setInds
  sqInds size 255 fill
  size 0 do
    i sqMap + c@  dim * sqInds + 1-
    begin 1+ dup c@ 255 = until
    i swap c!
  loop ;
setInds

create rowMask dim cells allot
create colMask dim cells allot
create  sqMask dim cells allot

: rowMaskAt ( n -- ^mask )  dim  /  cells rowMask + ;
: colMaskAt ( n -- ^mask )  dim mod cells colMask + ;
: sqMaskAt ( n -- ^mask ) sqMap + c@ cells sqMask + ;

: usedMask ( n -- mask )
  dup  rowMaskAt @
  over colMaskAt @ or
  swap  sqMaskAt @ or ;
: availMask
  usedMask [ dim 1+ pow2 1- 1- ] literal xor ;

create stack size 2* allot		\ stack used for backtracking
variable top
: initStack   stack top ! ;
: push   top @ c!  1 top +! ;
: pop   -1 top +!  top @ c@ ;
: .stack   stack begin dup top @ < while dup c@ . 1+ repeat drop ;

create puzzle size allot
: p@  puzzle + c@ ;
: p!  dup push  puzzle + c! ;
: 0p!  0 swap   puzzle + c! ;
: .board ( xt -- )
  size 0 do
    i sqSize mod 0= if
      i dim mod 0= if cr
        i dim 3 * = i dim 6 * = or if
          dim 2* 1+ 0 do [char] - emit loop cr
        then
      else space then
    then
    i over execute
  loop cr drop ;
: .sq ( i -- ) p@ . ;
: .puzzle  ['] .sq .board ;

: updateMasks ( mask i -- )
  2dup rowMaskAt +!
  2dup colMaskAt +!
        sqMaskAt +! ;
: addMasks ( i -- )
  dup p@ pow2 swap updateMasks ;

: initMasks
  rowMask dim cells erase colMask dim cells erase sqMask dim cells erase
  size 0 do
    i p@ if i addMasks then
  loop ;

: setPuzzle ( addr len -- ) puzzle fillWithString initMasks initStack ;

\ smarts

: findForced ( -- i T | F )
  size 0 do
    i p@ 0= if i availMask bits 1 = if i true unloop exit then then
  loop false ;

: findRow ( mask -1 -- mask i | -1 )
  dim 0 do  \ sqs
    over i cells rowMask + @ and 0= if
      i 1+ dim * dup dim - do
        i p@ 0= if
          over i colMaskAt @ i sqMaskAt @ or and 0= if
            dup 0< if drop i else drop -1 leave then
          then
        then
      loop
      dup 0< 0= if leave then
    then
  loop ;
: findCol ( mask -1 -- mask i | -1 )
  dim 0 do  \ sqs
    over i cells colMask + @ and 0= if
      size i do
        i p@ 0= if
          over i rowMaskAt @ i sqMaskAt @ or and 0= if
            dup 0< if drop i else drop -1 leave then
          then
        then
      dim +loop
      dup 0< 0= if leave then
    then
  loop ;
: findSq ( mask -1 -- mask i | -1 )
  dim 0 do
    over i cells sqMask + @ and 0= if
      i 1+ dim * sqInds + dup dim - do
        i c@ p@ 0= if
          over i c@ colMaskAt @ i c@ rowMaskAt @ or and 0= if
            dup 0< if drop i c@ else drop -1 leave then
          then
        then
      loop
      dup 0< 0= if leave then
    then
  loop ;
: findEliminated ( -- i n | 0 )
  dim 0 do            \ foreach number
    i 1+ pow2 -1 ( mask i )
    findRow dup 0< 0= if nip i 1+ unloop ( 2dup . .i ." row" cr ) exit then
    findCol dup 0< 0= if nip i 1+ unloop ( 2dup . .i ." col" cr ) exit then
    findSq  dup 0< 0= if nip i 1+ unloop ( 2dup . .i ." sq"  cr ) exit then
    2drop
  loop 0 ;

: allForcedMoves
  begin
    begin findForced while  \ dup .i ." forced" cr
      dup availMask 2/ log2
      over p! addMasks
      \ .puzzle key drop
    repeat
    findEliminated dup
  while
    over p! addMasks
    \ .puzzle key drop
  repeat drop ;

\ backtracking
\  stack of tried indexes
\  find index of smallest available (0: stuck, -1: done)

: findMostConstrained ( -- i possibilities )
  -1 9
  size 0 do
    i p@ 0= if i availMask bits over < if 2drop i i availMask bits then then
  loop ;

255 constant sentinal

: undoToSentinal
  begin pop dup sentinal <> while
    dup p@ pow2 negate over updateMasks  0p!
  repeat ( sentinal ) push ;

: solve
  allForcedMoves  \ no more forced moves? try some recursion and backtracking
  findMostConstrained 0= if ( ." dead end" cr) drop exit then
  dup 0< if drop ." solution:" .puzzle exit then
  dup availMask swap ( mask i )
  sentinal push
  dim 0 do
    over i pow2 and if  \ i . dup .i ." trying" .s
      i over p! dup addMasks
      \ .puzzle key drop
      recurse  \ ." undoing" .s cr
      undoToSentinal
    then
  loop
  pop ( sentinal ) drop 2drop ;

\ dev, testing, and examples

: xy>i  dim * + ;
: .i  dim /mod '( emit swap . . ') emit space ;

: time: ( params time: "word" -- ) \ gforth
  utime 2>r  ' execute  utime 2r> d-
  <# # # # # # # '. hold #s #> type ."  elapsed" ;

s" 960300800850010009003096020100000002000270180609084003006700050000028304040005060"
  setPuzzle \ mhx, easy: only requires findForced

.puzzle time: solve

s" 090004007000007900800000000405800000300000002000009706000000004003500000200600080"
  setPuzzle \ spykerman

.puzzle time: solve

: .ok ( n -- )
  availMask 10 1 do
    dup i pow2 and 0= if i . then
  loop drop ;
: .sqCount ( i -- ) dup p@ if drop 0 else availMask bits then . ;
: .counts  ['] .sqCount .board ;

: .nok ( mask xt i -- mask xt )
  dup p@ if drop 0 else availMask >r over r> and if 1 else 0 then then . ;
: .navail ( n -- ) pow2 ['] .nok .board drop ;

