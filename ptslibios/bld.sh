#/usr/bin/xcodebuild -project ptslibios.xcodeproj -target ptslibios -sdk iphonesimulator -arch ia64 -configuration Release clean build
#/usr/bin/xcodebuild -project ptslibios.xcodeproj -target ptslibios -sdk iphonesimulator -arch arm64 -configuration Release clean build
mkdir ULib
cd ULib
cp ../DerivedData/ptslibios/Build/Products/Debug-iphonesimulator/libptslibios.a ./libptslibios_x64.a
cp ../DerivedData/ptslibios/Build/Products/Debug-iphoneos/libptslibios.a ./libptslibios_arm64.a
lipo -create libptslibios_x64.a libptslibios_arm64.a -output libptslibios.a
cp libptslibios.a /Users/shanemorrison/Perforce/shanemac/dopple/IOSApp/Dopple/libptslibios.a