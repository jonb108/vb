#!/usr/bin/env perl
use strict;
use warnings;

use CGI;
my $q = CGI->new();
use DB_File;
use Date::Simple qw/
    date
/;
use List::MoreUtils qw/
    uniq
/;
use VB_SVG qw/
    msg2svg
/;

# TODO:
# 
# add messages for Thursday White W??
# a video
# command CT for type of connection
#   - pvc
#   - clips
#   - what else?
# super user commands - see bottom
# go national!
#     woodland and sac first
#     with different connection types
#
my $fmt   = "%m/%d/%y";
my $reserved_words = "name|title|password|attach_type|board_color";

sub JON {
    open my $out, '>>', '/tmp/jon';
    print {$out} "@_\n";
    close $out;
}

sub repl_quote {
    my ($s) = @_;
    $s =~ s{'}{JQXZ}xmsg;    # hack to avoid quoting issues
    $s =~ s{"}{ZXQJ}xmsg;    # hack to avoid quoting issues
    return $s;
}
sub rest_quote {
    my ($s) = @_;
    $s =~ s{JQXZ}{'}xmsg;    # hack to avoid quoting issues
    $s =~ s{ZXQJ}{"}xmsg;    # hack to avoid quoting issues
    return $s;
}

sub msgs {
    my ($date, $br_name, @messages) = @_;
    my @ret;
    for my $m (@messages) {
        push @ret, date($date)->format($fmt)
                 . ' '
                 . "<span class=green>$m</span>"
                 . ' '
                 . "<span class=gray>$br_name</span>"
                 . "<br>";
    }
    return @ret;
}

for my $d (qw/ brigades vblog /) {
    mkdir $d unless -d $d;
}

my $cmd = uc $q->param('cmd');
$cmd =~ s{\A \s* | \s* \z}{}xmsg;   # trim leading, trailing space
$cmd =~ s{ \s{2,} }{ }xmsg;         # no multiple space 

my $br = uc $q->param('br') || $q->cookie('brigade');
my $message = uc $q->param('message');
my $okay_message = '';
my %br;
my $msg = '';

TOP:
#
# first, which brigade are we dealing with?
#
if ($cmd =~ m{\A NEW \s+ (\S+) \s+ (.*) \z}xms) {
    my $name = $1;
    if (-f "brigades/$name.dbm") {
        $msg = "$name already exists";
    }
    else {
        my $password = $2;
        tie %br, 'DB_File', "brigades/$name.dbm";
        $br{name} = $name;
        $br{board_color} = 'BLACK';
        require Digest::SHA;
        $br{password} = Digest::SHA::sha256_hex($password);
        $br = $name;
    }
    $cmd = '';
}
elsif ($cmd =~ m{\A NEW \s+ (\S+) \z}xms) {
    my $name = $1;
    if (-f "brigades/$name.dbm") {
        $msg = "$name already exists";
    }
    else {
        tie %br, 'DB_File', "brigades/$name.dbm";
        require Digest::SHA;
        $br{name} = $name;
        $br{board_color} = 'BLACK';
        $br = $name;
    }
    $cmd = '';
}
elsif ($cmd =~ m{\A V \s+ (\S+) \z}xms) {
    my $name = $1;
    my $fname = "brigades/$name.dbm";
    if (-f $fname) {
        my %hash;
        tie %hash, 'DB_File', $fname;
        if (exists $hash{password}) {
            # we could prompt for it with
            # a <input type=password ...
            # but later...
            $msg = "missing password\n";
        }
        else {
            $br = $name;
            tie %br, 'DB_File', $fname;
        }
    }
    else {
        $msg = "unknown brigade: $name\n";
    }
    if ($msg && $br) {
        # the switch failed for some reason
        # so stay on the current one if there is one
        #
        tie %br, 'DB_File', "brigades/$br.dbm";
    }
    $cmd = '';
}
elsif ($cmd =~ m{\A V \s+ (\S+) \s+ (\S+) \z}xms) {
    my $name = $1;
    my $password = $2;
    if (-f "brigades/$name.dbm") {
        my %hash;
        tie %hash, 'DB_File', "brigades/$name.dbm";
        if (! exists $hash{password}) {
            $msg = "no password needed";
            $br = $name;
            tie %br, 'DB_File', "brigades/$br.dbm";
        }
        else {
            require Digest::SHA;
            if ($hash{password} eq Digest::SHA::sha256_hex($password)) {
                $br = $name;
                tie %br, 'DB_File', "brigades/$br.dbm";
            }
            else {
                $msg = "incorrect password\n";
            }
        }
    }
    else {
        $msg = "unknown brigade: $name\n";
    }
    if ($msg && $br) {
        # the switch failed for some reason
        # so stay on the current one if there is one
        #
        tie %br, 'DB_File', "brigades/$br.dbm";
    }
    $cmd = '';
}
elsif ($br) {
    # no $cmd to change the brigade or create a new one
    # we already have it from a hidden field
    # but check that it actually exists!
    if (! -f "brigades/$br.dbm") {
        $br = "";
    }
    else {
        tie %br, 'DB_File', "brigades/$br.dbm";
    }
}

# Now that we have a brigade
if ($br && $cmd && (length($cmd) <= 2) && exists $br{"$cmd="}) {
    # an alias expansion
    $cmd = $br{"$cmd="};
    # does the new command change the brigade??
    goto TOP;
}

# log it
if ($br && $cmd =~ m{\S}) {
    if (open my $log, '>>', "vblog/$br") {
        my ($min, $hour, $day, $month, $year)
            = (localtime(time - 60*60))[1 .. 5];
        printf {$log} "%02d/%02d/%02d %02d:%02d    $cmd\n",
                      $month+1, $day, $year%100, $hour, $min;
        close $log;
    }
}


my $nbsp = '&nbsp;' x 5;
sub add_it {
    my ($x) = @_;
    if (defined $x) {
        return "<td width=30 class=green>$x</td>"
             . "<td align=right width=10>$br{$x}$nbsp</td>";
    }
    else {
        return '<td></td><td></td>';
    }
}

if ($cmd =~ m{\A [+] \s* (\S|SP|HT|DN|UP) \z}xmsi) {
    my $let = $1;
    if (! $br) {
        $msg = "no brigade";
    }
    else {
        # increment the letter count
        my $n = ++$br{$let};
        $msg = "There "
             . (($n == 1)? "is": "are")
             . " now $n $let."
             ;
    }
}
elsif ($cmd =~ m{\A [-] \s* (\S|SP|HT|DN|UP) \z}xmsi) {
    my $let = $1;
    if (! $br) {
        $msg = "no brigade";
    }
    else {
        # decrement the letter count
        my $n = --$br{$let};
        $msg = "There "
             . (($n == 1)? "is": "are")
             . " now $n $let."
             ;
    }
}
elsif ($cmd =~ m{\A TL \s+ (.*) \z}xmsi) {
    if (! $br) {
        $msg = "no brigade";
    }
    else {
        $br{title} = $1;
    }
}
elsif ($cmd eq 'BC') {
    if (! $br) {
        $msg = "No brigade";
    }
    else {
        $msg = "board color is " . ucfirst lc $br{board_color};
    }
}
elsif ($cmd =~ m{\A BC \s+ (\S+) \z}xmsi) {
    my $bc = $1;
    if (! $br) {
        $msg = "no brigade";
    }
    elsif ($bc ne 'BLACK' && $bc ne 'WHITE') {
        $msg = 'board color is either BLACK or WHITE';
    }
    else {
        $br{board_color} = $bc;
        $msg = "board color set to " . ucfirst lc $bc;
    }
}
elsif ($cmd =~ m{\A PW \s+ (\S+) \z}xmsi) {
    if (! $br) {
        $msg = "no brigade";
    }
    else {
        require Digest::SHA;
        my $pwd = $1;
        if ($pwd eq '-') {
            delete $br{password};
            $msg = "password removed";
        }
        else {
            $br{password} = Digest::SHA::sha256_hex($pwd);
            $msg = "password added";
        }
    }
}
elsif ($cmd =~ m{\A BA \s (.*) \z}xmsi) {
    if (! $br) {
        $msg = "no brigade";
    }
    else {
        my $let_nums = $1;
        my @terms = split ' ', $let_nums;
        if (@terms % 2 != 0) {
            $msg = "odd number of parameters :(";
        }
        else {
            my $n = 0;
            TERM:
            while (@terms) {
                my $key = uc shift @terms;
                my $val = shift @terms;
                if ($key eq '~'
                    || (length $key != 1 && $key !~ m{SP|HT|DN|UP}xms)
                ) {
                    $msg = "illegal letter: $key";
                    last TERM;
                }
                elsif ($val !~ m{\A \d+ \z}xms) {
                    $msg = "illegal num: $val";
                    last TERM;
                }
                else {
                    $br{$key} = $val;
                    ++$n;
                }
            }
            if ($n) {
                if ($msg) {
                    $msg .= ', ';
                }
                $msg .= "$n added";
            }
        }
    }
}
elsif ($cmd eq 'L') {
    if (! $br) {
        $msg = "No brigade";
    }
    else {
        my @lets_am;
        my @lets_nz;
        my @digits;
        my @other;
        my $total = 0;
        LETS:
        for my $k (sort keys %br) {
            if ($k =~ m{\A ($reserved_words|\d{8}) \z}xms) {
                next LETS;
            }
            if ($k =~ m{\A [A-Z]{1,2} = \z}xms) {
                next LETS;
            }
            $total += $br{$k};
            if (length $k == 1 && ('A' le $k && $k le 'M')) {
                push @lets_am, $k;
            }
            elsif (length $k == 1 && ('N' le $k && $k le 'Z')) {
                push @lets_nz, $k;
            }
            elsif ('0' le $k && $k le '9') {
                push @digits, $k;
            }
            else {
                push @other, $k;
            }
        }
        $msg = "<ul><table border=0 cellpadding=5>\n";
        my $x;
        while (@lets_am || @lets_nz || @digits || @other) {
            $msg .= '<tr>';
            $msg .= add_it(shift @lets_am);
            $msg .= add_it(shift @lets_nz);
            $msg .= add_it(shift @digits);
            $msg .= add_it(shift @other);
            $msg .= "</tr>\n";
        }
        $msg .= "</table><p><span class=total>Total $total</span></ul>\n";
    }
}
elsif ($cmd =~ m{\A U \s+ ([\d/]+|T) \z}xms) {
    if (! $br) {
        $msg = "No brigade";
    }
    elsif (! $message) {
        $msg = "No current message";
    }
    else {
        my $dt = date(lc $1);
        if (! $dt) {
            $msg = "Invalid date: $1";
            # let them try again...
            $okay_message = "<input type=hidden name=message value='$message'>";
        }
        else {
            $br{$dt->as_d8()} .= rest_quote($message) . "~";
            $msg = "added on " . $dt->format($fmt);
        }
    }
}
elsif ($cmd =~ m{\A ([\d/]+|T) \s+ (.*) \z}xms) {
    if (! $br) {
        $msg = "No brigade";
    }
    else {
        my $date = $1;
        my $message = $2;
        my $dt = date(lc $date);
        if (! $dt) {
            $msg = "Invalid date: $date";
        }
        else {
            my $k = $dt->as_d8();
            my $d = $dt->format($fmt);
            if ($message eq '-') {
                if (exists $br{$k}) {
                    my $s = $br{$k};
                    $s =~ s{[^~]}{}xmsg;
                    my $n = length($s);
                    delete $br{$k};
                    if ($n == 1) {
                        $msg = "message on $d deleted";
                    }
                    else {
                        $msg = "$n messages on $d deleted";
                    }
                }
                else {
                    $msg = "There was no message on $d.";
                }
            }
            else {
                $br{$k} .= rest_quote($message) . "~";
                $msg = "message added on $d";
            }
        }
    }
}
elsif ($cmd eq 'H') {
    if (! $br) {
        $msg = "No brigade";
    }
    else {
        my $space = '&nbsp;' x 3;
        my $csv_file = "$br{name}.csv";
        open my $csv, '>', "../vb/$csv_file";
        print {$csv} "Date,Message\n";
        $msg = '';
        for my $d8 (sort { $b <=> $a }
                    grep { m{\A \d{8} \z}xms }
                    keys %br
        ) {
            my $dt = date($d8);
            for my $m (split '~', $br{$d8}) {
                $m = rest_quote($m);
                my $d = $dt->format($fmt);
                $msg .= "<span class=dow>"
                     .  $dt->format("%s")
                     .  "</span> "
                     .  $d
                     .  $space
                     .  "<span class=green>$m</span>"
                     .  "<br>"
                     ;
                $m =~ s{"}{""}xmsg;        # escape " by doubling it
                print {$csv} qq!$d,"$m"\n!;
            }
        }
        close $csv;
        $msg = "<div class=history>$msg</div>\n";
        $msg .= "<p><a href='/vb/$csv_file' download>CSV Export</a>\n";
    }
}
elsif ($cmd eq 'LG CLEAR') {
    if (! $br) {
        $msg = "No brigade";
    }
    else {
        open my $out, '>', "vblog/$br";
        close $out;
        $msg = "log cleared";
    }
}
elsif ($cmd eq 'LG') {
    if (! $br) {
        $msg = "No brigade";
    }
    else {
        if (open my $in, '<', "vblog/$br") {
            my @lines = <$in>;
            $msg = join '', reverse @lines;
            $msg = "<pre>$msg</pre>";
            close $in;
        }
    }
}
elsif ($cmd eq '~LB') {
    # super user
    $msg = <<'EOH';
<table cellpadding=5>
<tr>
<th align=left>Name</th>
<th align=left>Password?</th>
<th align=left>Title</th>
<th>Letters</th>
<th>Messages</th>
</tr>
EOH
    for my $b (<brigades/*.dbm>) {
        my %br;
        tie %br, 'DB_File', $b;
        my $nletters = 0;
        for my $l (grep {
                       !m{\A ($reserved_words|\d{8}) \z}xms
                   }
                   keys %br
        ) {
            $nletters += $br{$l};
        }
        my $nmessages = grep { m{\A \d{8} \z}xms } keys %br;
        my $pw = exists $br{password}? '*': '';
        $msg .= <<"EOH";
<tr>
<td>$br{name}</td>
<td>$pw</td>
<td>$br{title}</td>
<td align=right>$nletters</td>
<td align=right>$nmessages</td>
</tr>
EOH
    }
    $msg .= "</table>\n";
}
elsif ($cmd =~ m{\A ~VB \s+ (\S+) \z}xms) {
    # super user
    # no need for password
    my $name = $br = $1;
    my $fname = "brigades/$name.dbm";
    if (! -f $fname) {
        $msg = "$name: unknown brigade";
    }
    else {
        $br = $name;
        tie %br, 'DB_File', $fname;
    }
}
elsif ($cmd =~ m{\A ~RMB \s+ (\S+) \z}xms) {
    # super user
    # remove brigade
    my $name = $1;
    my $fname = "brigades/$name.dbm";
    if (! -f $fname) {
        $msg = "$name: unknown brigade";
    }
    else {
        unlink $fname;
        $msg = "removed";
        if ($br && $br eq $name) {
            untie %br;
            $br = '';
        }
    }
}
elsif ($cmd =~ m{\A ~RMP \s+ (\S+) \z}xms) {
    # super user
    # clear password
    my $br = $1;
    if (! -f "brigades/$br.dbm") {
        $msg = "$br: unknown brigade";
    }
    else {
        my %hash;
        tie %hash, 'DB_File', "brigades/$br.dbm";
        delete $hash{password};
        $msg = "password removed";
    }
}
elsif ($cmd eq '~LM') {
    # super user
    my @messages;
    for my $b (<brigades/*.dbm>) {
        my %hash;
        tie %hash, 'DB_File', $b;
        push @messages, map { split '~', $hash{$_} }
                        grep { m{\A \d{8} \z}xms }
                        keys %hash;
    }
    $msg = join '',
           map { "$_<br>\n" }
           sort { $a cmp $b }
           uniq
           @messages;
}
elsif ($cmd eq '~LMD') {
    # super user
    my @messages_aaref;
    for my $b (<brigades/*.dbm>) {
        my ($brigade_name) = $b =~ m{ / (.*).dbm }xms;
        my %hash;
        tie %hash, 'DB_File', $b;
        push @messages_aaref,
            map {
                [ $_, $brigade_name, split '~', $hash{$_} ]
            }
            grep { m{\A \d{8} \z}xms }
            keys %hash;
    }
    $msg = join '',
           map { msgs(@{$_}) }
           sort { $b->[0] <=> $a->[0] }
           @messages_aaref;
    $msg = "<div class=history>$msg</div>";
}
elsif ($cmd =~ m{\A ([A-Z]{1,2}) \s* = \s* (.*) \z}xms) {
    my $alias = $1;
    my $expansion = $2;
    #
    # an alias
    # but not with a built-in
    #
    my %is_a_builtin = map { $_ => 1 }
                       qw(
                           V TL L H BA PW U LG AL
                       );
    if ($is_a_builtin{$alias}) {
        $msg = "Cannot alias $alias because it is a built-in command.";
    }
    elsif (! $br) {
        $msg = "No brigade";
    }
    else {
        if ($expansion eq '-') {
            my $k = "$alias=";
            if (exists $br{$k}) {
                delete $br{$k};
                $msg = "deleted";
            }
            else {
                $msg = "no such alias";
            }
        }
        else {
            $br{"$alias="} = $expansion;
            $msg = "defined";
        }
    }
}
elsif ($cmd eq 'AL') {
    if (! $br) {
        $msg = "No brigade";
    }
    else {
        $msg = join "<br>\n",
               map { chop; "$_ => " . $br{"$_="} }
               sort
               grep { m{\A [A-Z]{1,2} = \z}xms }
               keys %br
               ;    # how fun is this? :)
    }
}
elsif ($cmd =~ m{\A RN \s+ (\S+) \z}xms) {
    my $new_name = $1;
    if (! $br) {
        $msg = "No brigade";
    }
    else {
        my $old_name = $br;
        untie %br;
        rename "brigades/$old_name.dbm", "brigades/$new_name.dbm";
        tie %br, 'DB_File', "brigades/$new_name.dbm";
        $br{name} = $new_name;
        $br = $new_name;
        $msg = "renamed";
    }
}
elsif ($cmd eq 'AT') {
    if (! $br) {
        $msg = "No brigade";
    }
    elsif (! exists $br{attach_type}) {
        $msg = "No attachment type.";
    }
    else {
        $msg = "The attachment type is $br{attach_type}.";
    }
}
elsif ($cmd =~ m{\A AT \s+ (\S+) \z}xms) {
    my $type = $1; 
    if (! $br) {
        $msg = "No brigade";
    }
    else {
        $br{attach_type} = $type;
    }
}
elsif ($cmd =~ m{\S}xms) {
    # testing a message for the bridge
    if (! $br) {
        $msg = "No brigade";
    }
    elsif ($cmd =~ m{~}xms) {
        $msg = "cannot use ~ in a message";
    }
    else {
        my $cmd1 = repl_quote($cmd);
        $msg = qq!<span class='big green' onclick='copy_to_clipboard("$cmd1")'>$cmd</span><span style='margin-left: .5in; color: gray;' id=copied></span><p>\n!;
        my %freq;
        for my $l (split //, $cmd) {
            if ($l eq ' ') {
                $l = 'SP';
            }
            ++$freq{$l};
        }
        my $err = 0;
        my $n_needed = 0;
        for my $l (sort keys %freq) {
            my $x = $freq{$l} - $br{$l};
            if ($x > 0) {
                $msg .= "Need $x more $l &#128546;";
                if ($l eq '0' && $br{O} >= ($freq{0} + $freq{O})) {
                    $msg .= " but there ARE enough O's";
                }
                elsif ($l eq '1' && $br{I} >= ($freq{1} + $freq{I})) {
                    $msg .= " but there ARE enough I's";
                }
                else {
                    $n_needed += $x;
                    $err = 1;
                }
                $msg .= "<br>\n";
            }
        }
        my $cmd2 = repl_quote($cmd);
        $okay_message = qq!<input type=hidden name=message value="$cmd2">!;
        my $nl = length $cmd;
        my $th = $n_needed == 1? "it is": "those are";
        my $ok = $err? "IF $th made..."
                :      "Okay &#128077; &#128522;";
        $msg .= "<p>$ok $nl letter boards"
             .  "<p><span class='fixed green2'>";
        if (exists $br{attach_type} && $br{attach_type} eq 'PVC') {
            # now to figure out the PVC pipe needs
            my ($T, $J, $I, $S) = (0, 0, 0, 0);

=comment
            # old code before Angie's SVG

            my $PVC;
            my $i = 0;
            while (my $l4 = substr($cmd, $i, 4)) {
                $msg .= "|$l4";
                $PVC .= "T";
                $i += 4;
                ++$T;
                my $len = length $l4;
                if ($len == 4) {
                    ++$J;
                    ++$I;
                    $PVC .= " JI ";
                }
                elsif ($len == 3) {
                    ++$J;
                    ++$S;
                    $PVC .= " JS";
                }
                elsif ($len == 2) {
                    ++$I;
                    $PVC .= "I ";
                }
                elsif ($len == 1) {
                    ++$S;
                    $PVC .= "S";
                }
            }
            $msg .= '|</span>';
            $PVC .= 'T';
            $msg .= "<br><span class=fixed>$PVC</span>";

=cut

            my $i = 0;
            while (my $l4 = substr($cmd, $i, 4)) {
                $i += 4;
                ++$T;
                my $len = length $l4;
                if ($len == 4) {
                    ++$J;
                    ++$I;
                }
                elsif ($len == 3) {
                    ++$J;
                    ++$S;
                }
                elsif ($len == 2) {
                    ++$I;
                }
                elsif ($len == 1) {
                    ++$S;
                }
            }
            ++$T;

            # Thanks to Angie!
            $msg .= msg2svg($cmd, $br{board_color} eq 'BLACK');

            $msg .= "<p>\n";
            $msg .= <<"EOH";
<ul><table cellpadding=5>
<tr><td>T</td><td align=right>$T</td></tr>
<tr><td>J</td><td align=right>$J</td></tr>
<tr><td>I</td><td align=right>$I</td></tr>
<tr><td>S</td><td align=right>$S</td></tr>
</table></ul>
EOH
        }
    }
}
my $br_cookie = $q->cookie(
                    -name    => 'brigade',
                    -value   => $br,
                    -expires => '+20y',
                );
print $q->header(-cookie => $br_cookie);
print <<'EOH';
<html>
<head>
<meta http-equiv="Cache-Control" content="no-cache">
<meta http-equiv="Pragma" content="no-cache">
<style>
body {
    margin: .5in;
}
body, input, pre {
    font-size: 18pt;
    font-family: Arial;
}
.cmd {
    text-transform: uppercase;
}
td, th {
    font-size: 24pt;
}
.fixed {
    font-size: 26pt;
    font-family: Courier;   /* fixed width */
}
.big {
    font-size: 30pt;
    cursor: pointer;
}
/* for message history */
.green {
    color: green;
    font-weight: bold;
}
.green2 {
    color: green;
    margin-left: 0in;
    font-weight: bold;
}
.gray {
    color: gray;
    margin-left: .3in;
    font-size: 14pt;
}
.total {
    font-size: 24pt;
}
a {
    text-decoration: none;
    color: blue;
}
.help {
    margin-left: 1in;
    font-size: 22pt;
    font-weight: normal;
}
.history {
    margin-left: 10mm;
    line-height: 9mm;
}
.dow {
    position: absolute;
    left: 12mm;
}
</style>
<script>
function set_focus() {
    var el = document.getElementById('cmd');
    el.select();
    el.focus();
}
function copy_to_clipboard(msg) {
    var msg1 = msg.replaceAll("JQXZ", "'");
    var msg2 = msg1.replaceAll("ZXQJ", '"');
    navigator.clipboard.writeText(msg2);
    set_focus();
    var el = document.getElementById('copied');
    el.innerHTML = 'copied';
    setTimeout(() => {
        el.innerHTML = "";
    }, 800);
}
</script>
</head>
<body>
<img src=/vb/antifa_fascism.jpg width=100%>
<h1>VISIBILITY BRIGADE
<a class=help target=_blank href=/vb/brigade_help.html>Help</a>
</h1>
EOH
if ($br) {
    print "<h2>$br{name}"
        . (exists $br{title}? " - $br{title}": '')
        . "</h2>\n";
}
my $tm = time;
print <<"EOH";
<form>
<input class=cmd type=text id=cmd name=cmd size=60 autocomplete=off>
<input type=hidden name=br value='$br'>
<input type=hidden name=tm value=$tm'>
$okay_message
</form>
EOH
print <<"EOH";
<p>
$msg
</body>
</html>
<script>
set_focus();
</script>
EOH

__END__

Undocumented super power user command:

X=HELLO

Now X is an alias for HELLO

YY = BLAH BLAH

Aliases are 1 or 2 letters A-Z.

Cannot alias a built-in command:
   V TL L H BA PW U LG AL

AL
    list the aliases
X=-
    delete the alias X

Likely useful *only* to easily change between different brigades
    that have long names and passwords like this:

in BRIG_WHITE (that has password ABRACADABRA):
X=V BRIG_BLACK SESAME

in BRIG_BLACK (that has password SESAME):
X=V BRIG_WHITE ABRACADABRA

Then X, X, X

===========

Super User commands:

~RMP <BRIGADE NAME>
    remove password - so they can reset it
~LB
    list all brigades with title, # letters, # messages
    and whether they have a password...
~RMB <BRIGADE NAME> Y
    remove the brigade
~VB <BRIGADE NAME>
    switch to the brigade without needing to provide a password
~LM
    list all unique messages in all brigades in alphabetical order
todo...
~LMD
    list all messages chronologically

    make them exportable with separate columns for date, message, brigade
