cd "$PSScriptRoot"  

# copy scripts
RoboCopy .\scripts .\github\scripts /MIR
RoboCopy .\scripts .\darkhan\scripts /MIR
RoboCopy .\scripts .\msdn\scripts /MIR
RoboCopy .\scripts .\msword\scripts /MIR

# copy topic templates
robocopy .\ .\github *.wcs /PURGE /xf _*.wcs
robocopy .\ .\msdn *.wcs /PURGE /xf _*.wcs 
robocopy .\ .\msword *.wcs /PURGE /xf _*.wcs