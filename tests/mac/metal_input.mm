#import <Foundation/Foundation.h>
#import <IOSurface/IOSurface.h>
#import <Metal/Metal.h>
#include <memory>

namespace {
struct Input {
    IOSurfaceRef surface { nullptr };
    id<MTLDevice> device;
    id<MTLTexture> texture;
    id<MTLCommandQueue> queue;
    ~Input() { if (surface) CFRelease(surface); }
};
}

extern "C" void* ranvier_test_metal_input_create(int width, int height, int wrongFormat)
{
    @autoreleasepool {
        if (width <= 0 || height <= 0 || width > 4096 || height > 4096)
            return nullptr;
        auto input = std::make_unique<Input>();
        input->device = MTLCreateSystemDefaultDevice();
        input->queue = [input->device newCommandQueue];
        if (!input->device || !input->queue)
            return nullptr;
        NSDictionary* attributes = @{
            (__bridge id)kIOSurfaceWidth: @(width), (__bridge id)kIOSurfaceHeight: @(height),
            (__bridge id)kIOSurfaceBytesPerElement: @4,
            (__bridge id)kIOSurfaceBytesPerRow: @(IOSurfaceAlignProperty(kIOSurfaceBytesPerRow, static_cast<size_t>(width) * 4)),
            (__bridge id)kIOSurfacePixelFormat: @(wrongFormat ? 0x52474241 : 0x42475241)
        };
        input->surface = IOSurfaceCreate((__bridge CFDictionaryRef)attributes);
        if (!input->surface)
            return nullptr;
        if (wrongFormat)
            return input.release();
        auto* descriptor = [MTLTextureDescriptor texture2DDescriptorWithPixelFormat:
            (wrongFormat ? MTLPixelFormatRGBA8Unorm : MTLPixelFormatBGRA8Unorm) width:width height:height mipmapped:NO];
        descriptor.usage = MTLTextureUsageRenderTarget | MTLTextureUsageShaderRead;
        descriptor.storageMode = MTLStorageModeShared;
        input->texture = [input->device newTextureWithDescriptor:descriptor iosurface:input->surface plane:0];
        if (!input->texture)
            return nullptr;
        return input.release();
    }
}

extern "C" void* ranvier_test_metal_input_surface(void* pointer)
{
    return pointer ? static_cast<Input*>(pointer)->surface : nullptr;
}

extern "C" int ranvier_test_metal_input_publish(void* pointer, int blue)
{
    @autoreleasepool {
        if (!pointer)
            return -1;
        auto& input = *static_cast<Input*>(pointer);
        auto* pass = [MTLRenderPassDescriptor renderPassDescriptor];
        pass.colorAttachments[0].texture = input.texture;
        pass.colorAttachments[0].loadAction = MTLLoadActionClear;
        pass.colorAttachments[0].storeAction = MTLStoreActionStore;
        pass.colorAttachments[0].clearColor = blue ? MTLClearColorMake(0, 0, 1, 1) : MTLClearColorMake(1, 0, 0, 1);
        id<MTLCommandBuffer> command = [input.queue commandBuffer];
        id<MTLRenderCommandEncoder> encoder = [command renderCommandEncoderWithDescriptor:pass];
        if (!command || !encoder)
            return -1;
        [encoder endEncoding];
        [command commit];
        [command waitUntilCompleted];
        return command.status == MTLCommandBufferStatusCompleted && !command.error ? 0 : -1;
    }
}

extern "C" void ranvier_test_metal_input_destroy(void* pointer)
{
    delete static_cast<Input*>(pointer);
}
